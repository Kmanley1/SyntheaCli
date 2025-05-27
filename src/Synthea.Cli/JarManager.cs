// JarManager.cs  — handles lazy download / caching of the latest Synthea JAR
// namespace: Synthea.Cli

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Synthea.Cli;

public static class JarManager
{
    private const string Repo    = "synthetichealth/synthea";
    private const string JarHint = "with-dependencies.jar";   // asset we want
    private const string ShaHint = ".sha256";                 // checksum (if provided)

    internal static HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            UserAgent = { ProductInfoHeaderValue.Parse("synthea-cli/0.1") }
        }
    };

    internal static string? CacheRootOverride { get; set; }

    /// <summary>
    /// Ensures the Synthea JAR is present in the local cache.
    /// Returns the full FileInfo.
    /// </summary>
    public static async Task<FileInfo> EnsureJarAsync(
        bool forceRefresh = false,
        IProgress<(long downloaded, long total)>? prog = null,
        CancellationToken token = default)
    {
        var cacheRoot = CacheRootOverride ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDir = Path.Combine(cacheRoot, "synthea-cli");

        Directory.CreateDirectory(cacheDir);

        // Re-use the newest cached JAR unless caller forces refresh
        var cached = Directory.GetFiles(cacheDir, $"*{JarHint}")
                              .OrderByDescending(File.GetLastWriteTimeUtc)
                              .FirstOrDefault();
        if (cached is not null && !forceRefresh)
            return new FileInfo(cached);

        // --- Query GitHub API for latest release ---
        var releaseJson = await Http.GetStringAsync(
            $"https://api.github.com/repos/{Repo}/releases/latest", token);

        using var doc = JsonDocument.Parse(releaseJson);
        var assets = doc.RootElement.GetProperty("assets");

        string? jarUrl = null;
        string? shaUrl = null;

        foreach (var a in assets.EnumerateArray())
        {
            var name = a.GetProperty("name").GetString();
            var url  = a.GetProperty("browser_download_url").GetString();
            if (name is null || url is null) continue;

            if (name.Contains(JarHint, StringComparison.OrdinalIgnoreCase))
                jarUrl = url;
            else if (name.EndsWith(ShaHint, StringComparison.OrdinalIgnoreCase))
                shaUrl = url;
        }

        if (jarUrl is null)
            throw new InvalidOperationException("Latest release did not contain a Synthea JAR.");

        var jarFile = Path.Combine(cacheDir, Path.GetFileName(jarUrl));

        // --- Download with progress to a temp file first ---
        var tmpFile = Path.GetTempFileName();
        try
        {
            await DownloadAsync(jarUrl, tmpFile, prog, token);

            // --- Optional checksum verification ---
            if (shaUrl is not null)
            {
                var expected = (await Http.GetStringAsync(shaUrl, token))
                               .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]
                               .Trim();
                var actual = await HashFileAsync(tmpFile, token);
                if (!expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Checksum mismatch for downloaded Synthea JAR.");
            }

            File.Move(tmpFile, jarFile, overwrite: true);
        }
        finally
        {
            // Clean up the temp file if anything went wrong
            try { File.Delete(tmpFile); } catch { }
        }


        return new FileInfo(jarFile);
    }

    private static async Task DownloadAsync(
        string url, string dest,
        IProgress<(long, long)>? prog,
        CancellationToken token)
    {
        // HttpResponseMessage implements IDisposable (not IAsyncDisposable) → plain using
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
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
