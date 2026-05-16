using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

        var jm = new JarManager(CreateClient(new(), new()), _tempDir);
        var fi = await jm.EnsureJarAsync();
        Assert.Equal(existing, fi.FullName);
    }

    [Fact]
    public async Task DownloadsJarWhenMissing()
    {
        var releaseJson = "{\"assets\":[{\"name\":\"synthea-with-dependencies.jar\",\"browser_download_url\":\"http://host/jar\"}]}";
        var jarBytes = new byte[] { 1, 2, 3 };
        var texts = new Dictionary<string, string> { { "https://api.github.com/repos/synthetichealth/synthea/releases/latest", releaseJson } };
        var bins = new Dictionary<string, byte[]> { { "http://host/jar", jarBytes } };

        var jm = new JarManager(CreateClient(texts, bins), _tempDir);
        var fi = await jm.EnsureJarAsync();
        Assert.True(File.Exists(fi.FullName));
        Assert.Equal(jarBytes, File.ReadAllBytes(fi.FullName));
    }

    [Fact]
    public async Task LogsDownloadProgress_AtInformation()
    {
        // Capturing logger provider observes JarManager's Information-level
        // entries during a download. The earlier static-Console.Write pattern
        // is gone; this pins the swap so a regression to silent or
        // Console-based output is caught. (A-11)
        var releaseJson = "{\"assets\":[{\"name\":\"synthea-with-dependencies.jar\",\"browser_download_url\":\"http://host/jar\"}]}";
        var texts = new Dictionary<string, string> { { "https://api.github.com/repos/synthetichealth/synthea/releases/latest", releaseJson } };
        var bins = new Dictionary<string, byte[]> { { "http://host/jar", new byte[] { 9, 9, 9 } } };

        var captured = new List<(LogLevel Level, string Message)>();
        using var factory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddProvider(new CapturingLoggerProvider(captured));
        });

        var jm = new JarManager(CreateClient(texts, bins), _tempDir, factory.CreateLogger<JarManager>());
        await jm.EnsureJarAsync();

        Assert.Contains(captured, e => e.Level == LogLevel.Information && e.Message.Contains("Synthea", StringComparison.Ordinal));
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

        var jm = new JarManager(CreateClient(texts, bins), _tempDir);
        await Assert.ThrowsAsync<InvalidOperationException>(() => jm.EnsureJarAsync());
    }

    [Fact]
    public async Task JarPathOverride_SkipsDownloadAndReturnsFile()
    {
        // Pre-create a fake "user-supplied" JAR outside the cache.
        var external = Path.Combine(_tempDir, "external.jar");
        await File.WriteAllTextAsync(external, "fake");
        // Use a handler that throws if any HTTP call is made — proves the
        // override truly skips the network.
        var jm = new JarManager(new HttpClient(new ThrowingHandler()), _tempDir);
        var fi = await jm.EnsureJarAsync(overrides: new JarOverrides(JarPath: external));
        Assert.Equal(external, fi.FullName);
    }

    [Fact]
    public async Task JarPathOverride_MissingFile_Throws()
    {
        var bogus = Path.Combine(_tempDir, "does-not-exist.jar");
        var jm = new JarManager(new HttpClient(new ThrowingHandler()), _tempDir);
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            jm.EnsureJarAsync(overrides: new JarOverrides(JarPath: bogus)));
    }

    [Fact]
    public async Task GitHubToken_AddsAuthorizationBearerHeader()
    {
        var releaseJson = "{\"assets\":[{\"name\":\"synthea-with-dependencies.jar\",\"browser_download_url\":\"http://host/jar\"}]}";
        var capture = new HeaderCapturingHandler(new Dictionary<string, string>
        {
            { "https://api.github.com/repos/synthetichealth/synthea/releases/latest", releaseJson }
        }, new Dictionary<string, byte[]> { { "http://host/jar", new byte[] { 1 } } });
        var jm = new JarManager(new HttpClient(capture), _tempDir);
        await jm.EnsureJarAsync(overrides: new JarOverrides(GitHubToken: "secret-tok"));

        Assert.Contains(capture.SeenAuthHeaders, h => h is { Scheme: "Bearer", Parameter: "secret-tok" });
    }

    [Fact]
    public async Task InsistChecksum_ThrowsWhenUpstreamHasNoSha()
    {
        var releaseJson = "{\"assets\":[{\"name\":\"synthea-with-dependencies.jar\",\"browser_download_url\":\"http://host/jar\"}]}";
        var texts = new Dictionary<string, string>
        {
            { "https://api.github.com/repos/synthetichealth/synthea/releases/latest", releaseJson }
        };
        var bins = new Dictionary<string, byte[]> { { "http://host/jar", new byte[] { 1 } } };
        var jm = new JarManager(CreateClient(texts, bins), _tempDir);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            jm.EnsureJarAsync(overrides: new JarOverrides(InsistChecksum: true)));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Unexpected network call to {request.RequestUri}");
    }

    private sealed class HeaderCapturingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _texts;
        private readonly Dictionary<string, byte[]> _bins;
        public List<System.Net.Http.Headers.AuthenticationHeaderValue?> SeenAuthHeaders { get; } = new();

        public HeaderCapturingHandler(Dictionary<string, string> texts, Dictionary<string, byte[]> bins)
        {
            _texts = texts;
            _bins = bins;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SeenAuthHeaders.Add(request.Headers.Authorization);
            var url = request.RequestUri!.ToString();
            if (_texts.TryGetValue(url, out var text))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
            if (_bins.TryGetValue(url, out var data))
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
                resp.Content.Headers.ContentLength = data.Length;
                return Task.FromResult(resp);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<(LogLevel Level, string Message)> _sink;
        public CapturingLoggerProvider(List<(LogLevel, string)> sink) => _sink = sink;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_sink);
        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly List<(LogLevel, string)> _sink;
            public CapturingLogger(List<(LogLevel, string)> sink) => _sink = sink;
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_sink) _sink.Add((logLevel, formatter(state, exception)));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
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
