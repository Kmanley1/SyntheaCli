using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class DoctorCheckTests : IDisposable
{
    private readonly string _tempDir;

    public DoctorCheckTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    // ----- JavaCheck ------------------------------------------------------

    [Fact]
    public async Task JavaCheck_Modern_ReturnsOk()
    {
        var check = new DoctorCommand.JavaCheck("java", new StubJavaDetector(new JavaProbeResult(true, 21, "21.0.5", null)));
        var r = await check.RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
        Assert.Contains("21", r.Message);
    }

    [Fact]
    public async Task JavaCheck_TooOld_ReturnsFail()
    {
        var check = new DoctorCommand.JavaCheck("java", new StubJavaDetector(new JavaProbeResult(true, 11, "11.0.20", null)));
        var r = await check.RunAsync(default);
        Assert.Equal(DoctorSeverity.Fail, r.Severity);
        Assert.Contains("17", r.Message);
    }

    [Fact]
    public async Task JavaCheck_NotFound_ReturnsFail()
    {
        var check = new DoctorCommand.JavaCheck("java", new StubJavaDetector(new JavaProbeResult(false, null, null, "not found")));
        var r = await check.RunAsync(default);
        Assert.Equal(DoctorSeverity.Fail, r.Severity);
    }

    // ----- CacheDirCheck --------------------------------------------------

    [Fact]
    public async Task CacheDirCheck_Writeable_ReturnsOk()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var fs = new StubFileSystem(canWrite: true);
        var r = await new DoctorCommand.CacheDirCheck(jar, fs).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
    }

    [Fact]
    public async Task CacheDirCheck_NotWriteable_ReturnsFail()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var fs = new StubFileSystem(canWrite: false, error: "permission denied");
        var r = await new DoctorCommand.CacheDirCheck(jar, fs).RunAsync(default);
        Assert.Equal(DoctorSeverity.Fail, r.Severity);
        Assert.Contains("permission denied", r.Message);
    }

    // ----- CachedJarCheck -------------------------------------------------

    [Fact]
    public async Task CachedJarCheck_NoneCached_ReturnsWarn()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var r = await new DoctorCommand.CachedJarCheck(jar, () => DateTime.UtcNow).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
        Assert.Contains("no JAR cached", r.Message);
    }

    // Container UX fix: an override JAR (--jar/SYNTHEA_CLI_JAR_PATH/config) is
    // used directly, so doctor reports OK even when the download cache is empty.
    [Fact]
    public async Task CachedJarCheck_OverrideJar_ReturnsOkWithPath()
    {
        var jarPath = Path.Combine(_tempDir, "baked.jar");
        File.WriteAllBytes(jarPath, new byte[2 * 1024 * 1024]);
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var r = await new DoctorCommand.CachedJarCheck(jar, () => DateTime.UtcNow, new FileInfo(jarPath)).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
        Assert.Contains(jarPath, r.Message);
        Assert.Contains("SYNTHEA_CLI_JAR_PATH", r.Message);
    }

    [Fact]
    public async Task CachedJarCheck_Fresh_ReturnsOk()
    {
        var jarPath = Path.Combine(_tempDir, "synthea-with-dependencies.jar");
        File.WriteAllBytes(jarPath, new byte[1024 * 1024]); // 1 MB
        File.SetLastWriteTimeUtc(jarPath, DateTime.UtcNow.AddDays(-5));
        var jar = new StubJarSource(_tempDir, new FileInfo(jarPath));
        var r = await new DoctorCommand.CachedJarCheck(jar, () => DateTime.UtcNow).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
    }

    [Fact]
    public async Task CachedJarCheck_Stale_ReturnsWarnWithRefreshHint()
    {
        var jarPath = Path.Combine(_tempDir, "synthea-with-dependencies.jar");
        File.WriteAllBytes(jarPath, new byte[1024]);
        File.SetLastWriteTimeUtc(jarPath, DateTime.UtcNow.AddDays(-200));
        var jar = new StubJarSource(_tempDir, new FileInfo(jarPath));
        var r = await new DoctorCommand.CachedJarCheck(jar, () => DateTime.UtcNow).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
        Assert.Contains("--refresh", r.Message);
    }

    [Fact]
    public async Task CachedJarCheck_Aging_ReturnsWarn()
    {
        var jarPath = Path.Combine(_tempDir, "synthea-with-dependencies.jar");
        File.WriteAllBytes(jarPath, new byte[1024]);
        File.SetLastWriteTimeUtc(jarPath, DateTime.UtcNow.AddDays(-120));
        var jar = new StubJarSource(_tempDir, new FileInfo(jarPath));
        var r = await new DoctorCommand.CachedJarCheck(jar, () => DateTime.UtcNow).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
        Assert.DoesNotContain("--refresh", r.Message);
    }

    // ----- ConfigCheck ----------------------------------------------------

    [Fact]
    public async Task ConfigCheck_Missing_ReturnsOk()
    {
        var path = Path.Combine(_tempDir, "no-config.json");
        var r = await new DoctorCommand.ConfigCheck(path).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
        Assert.Contains("no config file", r.Message);
    }

    [Fact]
    public async Task ConfigCheck_Valid_ReturnsOk()
    {
        var path = Path.Combine(_tempDir, "good.json");
        File.WriteAllText(path, """{ "jarPath": "/x/y.jar" }""");
        var r = await new DoctorCommand.ConfigCheck(path).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
        Assert.Contains("valid", r.Message);
    }

    [Fact]
    public async Task ConfigCheck_Malformed_ReturnsFail()
    {
        var path = Path.Combine(_tempDir, "broken.json");
        File.WriteAllText(path, "{not valid");
        var r = await new DoctorCommand.ConfigCheck(path).RunAsync(default);
        Assert.Equal(DoctorSeverity.Fail, r.Severity);
        Assert.Contains(path, r.Message);
    }

    // ----- GitHubCheck ----------------------------------------------------

    [Fact]
    public async Task GitHubCheck_Reachable_ReturnsOk()
    {
        var probe = new StubGitHubProbe(new GitHubReachabilityResult(true, 200, 198, null));
        var r = await new DoctorCommand.GitHubCheck(probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
        Assert.Contains("200", r.Message);
    }

    [Fact]
    public async Task GitHubCheck_Timeout_ReturnsWarn()
    {
        var probe = new StubGitHubProbe(new GitHubReachabilityResult(false, null, 5000, "timed out after 5.0s"));
        var r = await new DoctorCommand.GitHubCheck(probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
        Assert.Contains("--jar", r.Message);
    }

    [Fact]
    public async Task GitHubCheck_HttpError_ReturnsWarn()
    {
        var probe = new StubGitHubProbe(new GitHubReachabilityResult(false, 503, 100, null));
        var r = await new DoctorCommand.GitHubCheck(probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
        Assert.Contains("503", r.Message);
    }

    // ----- DiskSpaceCheck -------------------------------------------------

    [Fact]
    public async Task DiskSpaceCheck_Plenty_ReturnsOk()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var probe = new StubDiskSpaceProbe(45L * 1024 * 1024 * 1024); // 45 GB
        var r = await new DoctorCommand.DiskSpaceCheck(jar, probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Ok, r.Severity);
    }

    [Fact]
    public async Task DiskSpaceCheck_Tight_ReturnsWarn()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var probe = new StubDiskSpaceProbe(500L * 1024 * 1024); // 500 MB
        var r = await new DoctorCommand.DiskSpaceCheck(jar, probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
    }

    [Fact]
    public async Task DiskSpaceCheck_Tiny_ReturnsFail()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var probe = new StubDiskSpaceProbe(100L * 1024 * 1024); // 100 MB
        var r = await new DoctorCommand.DiskSpaceCheck(jar, probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Fail, r.Severity);
    }

    [Fact]
    public async Task DiskSpaceCheck_Unknown_ReturnsWarn()
    {
        var jar = new StubJarSource(_tempDir, cachedJar: null);
        var probe = new StubDiskSpaceProbe(null);
        var r = await new DoctorCommand.DiskSpaceCheck(jar, probe).RunAsync(default);
        Assert.Equal(DoctorSeverity.Warn, r.Severity);
    }

    // ----- Default file system probe (writeable smoke) --------------------

    [Fact]
    public void DefaultFileSystem_WriteableTemp_ReturnsTrue()
    {
        var fs = new DefaultFileSystem();
        Assert.True(fs.TryProbeWrite(_tempDir, out var err));
        Assert.Null(err);
    }

    // ----- Stubs ----------------------------------------------------------

    private sealed class StubJavaDetector : IJavaDetector
    {
        private readonly JavaProbeResult _result;
        public StubJavaDetector(JavaProbeResult result) => _result = result;
        public Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default)
            => Task.FromResult(_result);
    }

    private sealed class StubFileSystem : IFileSystem
    {
        private readonly bool _canWrite;
        private readonly string? _error;
        public StubFileSystem(bool canWrite, string? error = null)
        {
            _canWrite = canWrite;
            _error = error;
        }
        public bool TryProbeWrite(string directoryPath, out string? errorMessage)
        {
            errorMessage = _canWrite ? null : _error;
            return _canWrite;
        }
    }

    private sealed class StubJarSource : IJarSource
    {
        private readonly string _cachePath;
        private readonly FileInfo? _cachedJar;
        public StubJarSource(string cachePath, FileInfo? cachedJar)
        {
            _cachePath = cachePath;
            _cachedJar = cachedJar;
        }
        public string CachePath => _cachePath;
        public FileInfo? TryFindCachedJar() => _cachedJar;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => Task.FromResult(_cachedJar ?? throw new InvalidOperationException("No JAR cached"));
    }

    private sealed class StubGitHubProbe : IGitHubReachabilityProbe
    {
        private readonly GitHubReachabilityResult _result;
        public StubGitHubProbe(GitHubReachabilityResult result) => _result = result;
        public Task<GitHubReachabilityResult> PingAsync(TimeSpan timeout, CancellationToken cancelToken)
            => Task.FromResult(_result);
    }

    private sealed class StubDiskSpaceProbe : IDiskSpaceProbe
    {
        private readonly long? _free;
        public StubDiskSpaceProbe(long? free) => _free = free;
        public long? GetFreeBytes(string path) => _free;
    }
}
