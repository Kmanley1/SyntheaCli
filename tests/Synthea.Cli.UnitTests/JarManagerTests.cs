using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class JarManagerTests : IDisposable
{
    private readonly string _tempDir;
    public JarManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        JarManager.CacheRootOverride = _tempDir;
        Environment.SetEnvironmentVariable("TMPDIR", _tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static HttpClient CreateClient(Dictionary<string, string> texts, Dictionary<string, byte[]> binaries)
    {
        return new HttpClient(new StubHandler(texts, binaries));
    }

    [Fact]
    public async Task ReturnsCachedFileWhenPresent()
    {
        var cache = Path.Combine(_tempDir, "Synthea.Cli");
        Directory.CreateDirectory(cache);
        var existing = Path.Combine(cache, "cached-with-dependencies.jar");
        await File.WriteAllTextAsync(existing, "dummy");
        JarManager.Http = CreateClient(new(), new());

        var fi = await JarManager.EnsureJarAsync();
        Assert.Equal(existing, fi.FullName);
    }

    [Fact]
    public async Task DownloadsJarWhenMissing()
    {
        var releaseJson = "{\"assets\":[{\"name\":\"synthea-with-dependencies.jar\",\"browser_download_url\":\"http://host/jar\"}]}";
        var jarBytes = new byte[] { 1, 2, 3 };
        var texts = new Dictionary<string, string> { { "https://api.github.com/repos/synthetichealth/synthea/releases/latest", releaseJson } };
        var bins = new Dictionary<string, byte[]> { { "http://host/jar", jarBytes } };
        JarManager.Http = CreateClient(texts, bins);

        var fi = await JarManager.EnsureJarAsync();
        Assert.True(File.Exists(fi.FullName));
        Assert.Equal(jarBytes, File.ReadAllBytes(fi.FullName));
    }

    [Fact]
    public async Task ThrowsWhenChecksumMismatch()
    {
        var releaseJson = "{\"assets\":[{\"name\":\"synthea-with-dependencies.jar\",\"browser_download_url\":\"http://host/jar\"},{\"name\":\"synthea.jar.sha256\",\"browser_download_url\":\"http://host/jar.sha\"}]}";
        var jarBytes = new byte[] { 4, 5, 6 };
        var texts = new Dictionary<string, string>
        {
            {"https://api.github.com/repos/synthetichealth/synthea/releases/latest", releaseJson},
            {"http://host/jar.sha", "deadbeef"}
        };
        var bins = new Dictionary<string, byte[]> { { "http://host/jar", jarBytes } };
        JarManager.Http = CreateClient(texts, bins);

        await Assert.ThrowsAsync<InvalidOperationException>(() => JarManager.EnsureJarAsync());
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _texts;
        private readonly Dictionary<string, byte[]> _bins;
        public StubHandler(Dictionary<string, string> texts, Dictionary<string, byte[]> bins)
        {
            _texts = texts;
            _bins = bins;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (_texts.TryGetValue(url, out var text))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
            }
            if (_bins.TryGetValue(url, out var data))
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                resp.Content.Headers.ContentLength = data.Length;
                return Task.FromResult(resp);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }
}
