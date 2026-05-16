// JarManager.cs  — handles lazy download / caching of the latest Synthea JAR
// namespace: Synthea.Cli

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Synthea.Cli;

// Per-invocation overrides resolved from CLI > env > config > default.
// Bundled into one record so EnsureJarAsync stays single-purpose and
// callers don't need to thread four parameters everywhere. (A-5, A-36, A-40)
//
// Token applies as a per-request Authorization header so it works with any
// HttpClient including stub-handler test clients. Proxy is NOT here — it
// must be configured on the HttpClient handler at construction time
// (see HttpClientFactory), because HttpClient does not expose its handler
// for runtime mutation. Program wires HTTPS_PROXY / HTTP_PROXY env vars
// into the default client there.
internal sealed record JarOverrides(
    string? JarPath = null,
    string? GitHubToken = null,
    bool InsistChecksum = false);

internal interface IJarSource
{
    FileInfo? TryFindCachedJar();
    Task<FileInfo> EnsureJarAsync(
        bool forceRefresh = false,
        IProgress<(long downloaded, long total)>? prog = null,
        CancellationToken token = default,
        JarOverrides? overrides = null);
}

internal sealed class JarManager : IJarSource
{
    private const string Repo = "synthetichealth/synthea";
    private const string JarHint = "with-dependencies.jar";   // asset we want
    private const string ShaHint = ".sha256";                 // checksum (if provided)
    private static readonly ProductInfoHeaderValue UserAgent =
        ProductInfoHeaderValue.Parse("Synthea.Cli/0.1");

    private static HttpClient CreateDefaultClient() => new()
    {
        DefaultRequestHeaders =
        {
            UserAgent = { UserAgent }
        }
    };

    private readonly HttpClient _http;
    private readonly string? _cacheRootOverride;
    private readonly ILogger<JarManager> _logger;

    public JarManager()
        : this(http: null, cacheRootOverride: null, logger: null)
    {
    }

    public JarManager(HttpClient? http = null, string? cacheRootOverride = null, ILogger<JarManager>? logger = null)
    {
        _http = http ?? CreateDefaultClient();
        _cacheRootOverride = cacheRootOverride;
        _logger = logger ?? NullLogger<JarManager>.Instance;
    }

    /// <summary>
    /// Returns the newest cached Synthea JAR if one exists, without any
    /// network call. Returns null if the cache is empty or missing.
    /// </summary>
    public FileInfo? TryFindCachedJar()
    {
        var cacheDir = ResolveCacheDir();
        if (!Directory.Exists(cacheDir)) return null;
        var cached = Directory.GetFiles(cacheDir, $"*{JarHint}")
                              .OrderByDescending(File.GetLastWriteTimeUtc)
                              .FirstOrDefault();
        return cached is null ? null : new FileInfo(cached);
    }

    /// <summary>
    /// Ensures the Synthea JAR is present in the local cache.
    /// Returns the full FileInfo.
    /// </summary>
    public async Task<FileInfo> EnsureJarAsync(
        bool forceRefresh = false,
        IProgress<(long downloaded, long total)>? prog = null,
        CancellationToken token = default,
        JarOverrides? overrides = null)
    {
        overrides ??= new JarOverrides();

        // --- (A-5) --jar / SYNTHEA_CLI_JAR_PATH: skip download entirely ---
        if (!string.IsNullOrEmpty(overrides.JarPath))
        {
            if (!File.Exists(overrides.JarPath))
                throw new FileNotFoundException(
                    $"Synthea JAR override path does not exist: {overrides.JarPath}",
                    overrides.JarPath);
            _logger.LogInformation("Using user-supplied Synthea JAR at {Path}", overrides.JarPath);
            return new FileInfo(overrides.JarPath);
        }

        var cacheDir = ResolveCacheDir();
        Directory.CreateDirectory(cacheDir);

        // Re-use the newest cached JAR unless caller forces refresh
        var cached = Directory.GetFiles(cacheDir, $"*{JarHint}")
                              .OrderByDescending(File.GetLastWriteTimeUtc)
                              .FirstOrDefault();
        if (cached is not null && !forceRefresh)
        {
            _logger.LogDebug("Using cached Synthea JAR at {Path}", cached);
            return new FileInfo(cached);
        }

        // --- Query GitHub API for latest release ---
        _logger.LogInformation("Querying GitHub for the latest Synthea release");
        var releaseJson = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{Repo}/releases/latest", overrides.GitHubToken, token);

        using var doc = JsonDocument.Parse(releaseJson);
        if (!doc.RootElement.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Unexpected GitHub release response shape: missing 'assets' array. " +
                "GitHub may have changed its API, or the latest release is malformed.");
        }

        string? jarUrl = null;
        string? shaUrl = null;

        foreach (var a in assets.EnumerateArray())
        {
            if (!a.TryGetProperty("name", out var nameEl) ||
                !a.TryGetProperty("browser_download_url", out var urlEl))
                continue;
            var name = nameEl.GetString();
            var url = urlEl.GetString();
            if (name is null || url is null) continue;

            if (name.Contains(JarHint, StringComparison.OrdinalIgnoreCase))
                jarUrl = url;
            else if (name.EndsWith(ShaHint, StringComparison.OrdinalIgnoreCase))
                shaUrl = url;
        }

        if (jarUrl is null)
            throw new InvalidOperationException("Latest release did not contain a Synthea JAR.");

        // --- (A-36) --insist-checksum: refuse to download if no .sha256 ---
        if (shaUrl is null && overrides.InsistChecksum)
            throw new InvalidOperationException(
                "Upstream release does not publish a .sha256 asset, and --insist-checksum was set. " +
                "Re-run without --insist-checksum to accept the JAR unverified, or wait for an upstream release with a checksum.");

        var jarFile = Path.Combine(cacheDir, Path.GetFileName(jarUrl));

        // Download with progress to a temp file first. GetRandomFileName() is
        // preferred over GetTempFileName() — the latter has a 65,535-name
        // ceiling and creates a 0-byte file as a side effect.
        var tmpFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            _logger.LogInformation("Downloading Synthea JAR from {Url}", jarUrl);
            await DownloadAsync(_http, jarUrl, tmpFile, prog, overrides.GitHubToken, token);

            // --- Optional checksum verification ---
            if (shaUrl is not null)
            {
                var expected = (await SendGitHubGetAsync(shaUrl, overrides.GitHubToken, token))
                               .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
                               .Trim();
                var actual = await HashFileAsync(tmpFile, token);
                if (!expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Checksum mismatch for downloaded Synthea JAR.");
            }

            File.Move(tmpFile, jarFile, overwrite: true);
            _logger.LogInformation("Cached Synthea JAR at {Path}", jarFile);
        }
        finally
        {
            // Cleanup is best-effort: the temp file may already be gone (the
            // Move above succeeded) or locked by AV. Narrow catch so anything
            // unexpected (OOM, etc.) still propagates.
            try { File.Delete(tmpFile); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return new FileInfo(jarFile);
    }

    private string ResolveCacheDir()
    {
        var cacheRoot = _cacheRootOverride ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(cacheRoot, "Synthea.Cli");
    }

    private async Task<string> SendGitHubGetAsync(string url, string? gitHubToken, CancellationToken token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(gitHubToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gitHubToken);
        using var resp = await _http.SendAsync(req, token);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(token);
    }

    private static async Task DownloadAsync(
        HttpClient http, string url, string dest,
        IProgress<(long, long)>? prog,
        string? gitHubToken,
        CancellationToken token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(gitHubToken))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gitHubToken);
        // HttpResponseMessage implements IDisposable (not IAsyncDisposable) → plain using
        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? -1L;

        // Stream is IDisposable too
        using var src = await resp.Content.ReadAsStreamAsync(token);
        await using var dst = File.Create(dest);                    // FileStream *is* IAsyncDisposable

        var buffer = new byte[81920];
        long readSoFar = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, token)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), token);
            readSoFar += read;
            prog?.Report((readSoFar, total));
        }
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using var fs = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(fs, token);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
