using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class JarManagerTests : IDisposable
{
    // JarManager downloads/verifies exactly this pinned release (no GitHub API
    // call). Tests stub this URL and inject the SHA of their stub bytes.
    private const string PinnedUrl =
        "https://github.com/synthetichealth/synthea/releases/download/v4.0.0/synthea-with-dependencies.jar";
    private const string PinnedCacheName = "synthea-v4.0.0-with-dependencies.jar";

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
        => new(new StubHandler(texts, binaries));

    private static string Sha256Hex(byte[] data)
        => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    [Fact]
    public async Task ReturnsCachedFileWhenPresent()
    {
        var cache = Path.Combine(_tempDir, "Synthea.Cli");
        Directory.CreateDirectory(cache);
        var existing = Path.Combine(cache, PinnedCacheName);
        await File.WriteAllTextAsync(existing, "dummy");

        // A handler that throws on any HTTP call proves the cache hit skips the network.
        var jm = new JarManager(new HttpClient(new ThrowingHandler()), _tempDir);
        var fi = await jm.EnsureJarAsync();
        Assert.Equal(existing, fi.FullName);
    }

    [Fact]
    public async Task DownloadsAndVerifiesPinnedJar_WhenMissing()
    {
        var jarBytes = new byte[] { 1, 2, 3 };
        var bins = new Dictionary<string, byte[]> { { PinnedUrl, jarBytes } };

        var jm = new JarManager(CreateClient(new(), bins), _tempDir, syntheaSha256: Sha256Hex(jarBytes));
        var fi = await jm.EnsureJarAsync();

        Assert.True(File.Exists(fi.FullName));
        Assert.Equal(PinnedCacheName, fi.Name);
        Assert.Equal(jarBytes, File.ReadAllBytes(fi.FullName));
    }

    [Fact]
    public async Task LogsDownloadProgress_AtInformation()
    {
        // Capturing logger provider observes JarManager's Information-level
        // entries during a download. Pins the swap away from Console output. (A-11)
        var jarBytes = new byte[] { 9, 9, 9 };
        var bins = new Dictionary<string, byte[]> { { PinnedUrl, jarBytes } };

        var captured = new List<(LogLevel Level, string Message)>();
        using var factory = LoggerFactory.Create(b =>
        {
            b.SetMinimumLevel(LogLevel.Trace);
            b.AddProvider(new CapturingLoggerProvider(captured));
        });

        var jm = new JarManager(CreateClient(new(), bins), _tempDir, factory.CreateLogger<JarManager>(),
                                syntheaSha256: Sha256Hex(jarBytes));
        await jm.EnsureJarAsync();

        Assert.Contains(captured, e => e.Level == LogLevel.Information && e.Message.Contains("Synthea", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThrowsWhenDownloadFailsIntegrityCheck()
    {
        var jarBytes = new byte[] { 4, 5, 6 };
        var bins = new Dictionary<string, byte[]> { { PinnedUrl, jarBytes } };

        // Inject a SHA that does NOT match the downloaded bytes → integrity failure,
        // unconditionally (no opt-in flag). A tampered download must never be cached.
        var jm = new JarManager(CreateClient(new(), bins), _tempDir, syntheaSha256: "deadbeef");
        await Assert.ThrowsAsync<InvalidOperationException>(() => jm.EnsureJarAsync());
        Assert.False(File.Exists(Path.Combine(_tempDir, "Synthea.Cli", PinnedCacheName)));
    }

    [Fact]
    public async Task JarPathOverride_SkipsDownloadAndReturnsFile()
    {
        // Pre-create a fake "user-supplied" JAR outside the cache.
        var external = Path.Combine(_tempDir, "external.jar");
        await File.WriteAllTextAsync(external, "fake");
        // A handler that throws on any HTTP call proves the override skips the network.
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
        var jarBytes = new byte[] { 1 };
        var capture = new HeaderCapturingHandler(new(), new Dictionary<string, byte[]> { { PinnedUrl, jarBytes } });
        var jm = new JarManager(new HttpClient(capture), _tempDir, syntheaSha256: Sha256Hex(jarBytes));
        await jm.EnsureJarAsync(overrides: new JarOverrides(GitHubToken: "secret-tok"));

        Assert.Contains(capture.SeenAuthHeaders, h => h is { Scheme: "Bearer", Parameter: "secret-tok" });
    }

    [Fact]
    public async Task Download_RetriesTransientFailure_ThenSucceeds()
    {
        var jarBytes = new byte[] { 7, 7, 7 };
        var handler = new FlakyHandler(failFirst: 1, url: PinnedUrl, payload: jarBytes);
        var jm = new JarManager(new HttpClient(handler), _tempDir, syntheaSha256: Sha256Hex(jarBytes));

        var fi = await jm.EnsureJarAsync();

        Assert.True(File.Exists(fi.FullName));
        Assert.Equal(2, handler.Attempts);   // failed once, succeeded on the retry
    }

    private sealed class FlakyHandler : HttpMessageHandler
    {
        private readonly int _failFirst;
        private readonly string _url;
        private readonly byte[] _payload;
        public int Attempts { get; private set; }

        public FlakyHandler(int failFirst, string url, byte[] payload)
        {
            _failFirst = failFirst;
            _url = url;
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.ToString() != _url)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            Attempts++;
            if (Attempts <= _failFirst)
                throw new HttpRequestException("simulated transient failure");
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_payload) };
            resp.Content.Headers.ContentLength = _payload.Length;
            return Task.FromResult(resp);
        }
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

    private sealed class StubHandler : HttpMessageHandler
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
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(text) });
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
