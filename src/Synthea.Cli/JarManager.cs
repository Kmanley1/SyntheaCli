// JarManager.cs  — handles lazy download / caching of the latest Synthea JAR
// namespace: Synthea.Cli

using System.Net.Http.Headers;
using System.Security.Cryptography;
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
    // Absolute path to the directory where this source caches JARs. Used by
    // CacheCommand for `synthea cache list/clear` without requiring callers
    // to know how the path was resolved.
    string CachePath { get; }
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
    private const string JarHint = "with-dependencies.jar";   // asset name suffix

    // Pinned Synthea release + its known-good SHA-256. The binary downloads and
    // verifies exactly this release (the Docker image bakes the same one), so the
    // default `dotnet tool` path runs a reproducible, integrity-checked engine
    // instead of GitHub's MUTABLE /releases/latest rolling build. To run a
    // different engine, pass --jar with your own JAR.
    internal const string PinnedSyntheaVersion = "v4.0.0";
    internal const string PinnedSyntheaSha256 =
        "ed43c20ad40ba5c3bc724503a5af032715fe3c491620b766148e7c2361e6ecc1";
    private static string PinnedJarUrl =>
        $"https://github.com/{Repo}/releases/download/{PinnedSyntheaVersion}/synthea-with-dependencies.jar";
    private static string PinnedJarFileName => $"synthea-{PinnedSyntheaVersion}-{JarHint}";

    private static readonly ProductInfoHeaderValue UserAgent =
        new("synthea-cli", Program.GetCliVersion());

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
    private readonly string _expectedSha256;

    public JarManager()
        : this(http: null, cacheRootOverride: null, logger: null)
    {
    }

    // syntheaSha256: the expected hash to verify the download against. Defaults to
    // the pinned release's hash; injectable so tests can verify a stub JAR.
    public JarManager(HttpClient? http = null, string? cacheRootOverride = null, ILogger<JarManager>? logger = null,
                      string? syntheaSha256 = null)
    {
        _http = http ?? CreateDefaultClient();
        _cacheRootOverride = cacheRootOverride;
        _logger = logger ?? NullLogger<JarManager>.Instance;
        _expectedSha256 = syntheaSha256 ?? PinnedSyntheaSha256;
    }

    public string CachePath => ResolveCacheDir();

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

        // Re-use the pinned, already-verified cache file unless forced to refresh.
        var jarFile = new FileInfo(Path.Combine(cacheDir, PinnedJarFileName));
        if (jarFile.Exists && !forceRefresh)
        {
            _logger.LogDebug("Using cached Synthea JAR at {Path}", jarFile.FullName);
            return jarFile;
        }

        // Download the pinned release to a temp file, verify its SHA-256 against
        // the expected (baked-in) value, then atomically promote it into the
        // cache. A tampered or corrupt download fails verification and is never
        // moved into the cache or run. GetRandomFileName() avoids GetTempFileName's
        // 65,535-name ceiling and 0-byte side-effect.
        var tmpFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            _logger.LogInformation("Downloading Synthea {Version} from {Url}", PinnedSyntheaVersion, PinnedJarUrl);
            await DownloadAsync(_http, PinnedJarUrl, tmpFile, prog, overrides.GitHubToken, token);

            var actual = await HashFileAsync(tmpFile, token);
            if (!_expectedSha256.Equals(actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Synthea JAR integrity check failed: expected SHA-256 {_expectedSha256}, got {actual}. " +
                    "The download may be corrupt or tampered — refusing to run it.");

            File.Move(tmpFile, jarFile.FullName, overwrite: true);
            _logger.LogInformation("Cached verified Synthea JAR at {Path}", jarFile.FullName);
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

        jarFile.Refresh();
        return jarFile;
    }

    private string ResolveCacheDir()
    {
        var cacheRoot = _cacheRootOverride ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(cacheRoot, "Synthea.Cli");
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
