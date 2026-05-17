using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class DoctorCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StubJarSource _jarSource;
    private readonly StubJavaDetector _java = new();
    private readonly StubFileSystem _fs = new();
    private readonly StubGitHubProbe _gitHub = new();
    private readonly StubDiskSpaceProbe _disk = new();
    private readonly ServiceProvider _services;

    public DoctorCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _jarSource = new StubJarSource(_tempDir);
        _disk.FreeBytes = 50L * 1024 * 1024 * 1024;

        var sc = new ServiceCollection();
        sc.AddSingleton<IProcessRunner>(new NoopProcessRunner());
        sc.AddSingleton<IJarSource>(_jarSource);
        sc.AddSingleton<IJavaDetector>(_java);
        sc.AddSingleton<IFileSystem>(_fs);
        sc.AddSingleton<IGitHubReachabilityProbe>(_gitHub);
        sc.AddSingleton<IDiskSpaceProbe>(_disk);
        _services = sc.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task Doctor_AllOk_ReturnsExitZero()
    {
        var code = await Program.RunAsync(new[] { "doctor" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Doctor_JavaTooOld_ReturnsExitOne()
    {
        _java.Result = new JavaProbeResult(true, 11, "11.0.20", null);
        var code = await Program.RunAsync(new[] { "doctor" }, _services);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Doctor_OnlyWarnings_ReturnsExitZero()
    {
        _jarSource.CachedJar = null; // → Warn ("no JAR cached")
        _gitHub.Result = new GitHubReachabilityResult(false, null, 5000, "timed out");
        var code = await Program.RunAsync(new[] { "doctor" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Doctor_FailOverridesWarn_ReturnsExitOne()
    {
        _jarSource.CachedJar = null; // Warn
        _disk.FreeBytes = 100L * 1024 * 1024; // Fail
        var code = await Program.RunAsync(new[] { "doctor" }, _services);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task Doctor_HonorsRootJavaPathOption()
    {
        var code = await Program.RunAsync(new[] { "--java-path", "/opt/jdk21/bin/java", "doctor" }, _services);
        Assert.Equal(0, code);
        Assert.Equal("/opt/jdk21/bin/java", _java.LastPath);
    }

    // Golden-shape check: the table has aligned columns and a summary line.
    [Fact]
    public void FormatReport_AllOk_HasAlignedColumnsAndPassedSummary()
    {
        var results = new List<DoctorCheckResult>
        {
            new("Java", DoctorSeverity.Ok, "Java 21"),
            new("Cache dir", DoctorSeverity.Ok, "writeable"),
            new("Cached JAR", DoctorSeverity.Ok, "5d old"),
            new("Config", DoctorSeverity.Ok, "valid"),
            new("GitHub", DoctorSeverity.Ok, "200 OK"),
            new("Disk space", DoctorSeverity.Ok, "42 GB free"),
        };
        var report = DoctorCommand.FormatReport(results);
        Assert.Contains("synthea doctor", report);
        Assert.Contains("All checks passed.", report);
        // Every status line starts with one of the three severity tags.
        var lines = report.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.StartsWith("["))
            .ToArray();
        Assert.Equal(6, lines.Length);
        Assert.All(lines, l => Assert.StartsWith("[ OK ]", l));
        // Column 2 (the name) starts at the same byte offset on every line.
        var nameStarts = lines.Select(l => l.IndexOf("J") >= 0 ? l.IndexOf(l.Trim()[6]) : -1).ToArray();
        var nameColumn = lines.Select(l => l.Substring("[ OK ]".Length).TakeWhile(c => c == ' ').Count()).Distinct();
        Assert.Single(nameColumn);
    }

    [Fact]
    public void FormatReport_FailPresent_SummaryReportsCount()
    {
        var results = new List<DoctorCheckResult>
        {
            new("Java", DoctorSeverity.Fail, "Java 11"),
            new("Disk space", DoctorSeverity.Fail, "low"),
            new("Cache dir", DoctorSeverity.Ok, "ok"),
        };
        var report = DoctorCommand.FormatReport(results);
        Assert.Contains("2 critical issues", report);
    }

    [Fact]
    public void FormatReport_WarnOnly_SummaryReportsCount()
    {
        var results = new List<DoctorCheckResult>
        {
            new("Cached JAR", DoctorSeverity.Warn, "none cached"),
            new("Java", DoctorSeverity.Ok, "21"),
        };
        var report = DoctorCommand.FormatReport(results);
        Assert.Contains("1 warning", report);
        Assert.Contains("still run", report);
    }

    [Fact]
    public void ExitCodeFor_AllOk_IsZero()
    {
        var code = DoctorCommand.ExitCodeFor(new[]
        {
            new DoctorCheckResult("a", DoctorSeverity.Ok, ""),
            new DoctorCheckResult("b", DoctorSeverity.Ok, ""),
        });
        Assert.Equal(0, code);
    }

    [Fact]
    public void ExitCodeFor_WarnOnly_IsZero()
    {
        var code = DoctorCommand.ExitCodeFor(new[]
        {
            new DoctorCheckResult("a", DoctorSeverity.Ok, ""),
            new DoctorCheckResult("b", DoctorSeverity.Warn, ""),
        });
        Assert.Equal(0, code);
    }

    [Fact]
    public void ExitCodeFor_AnyFail_IsOne()
    {
        var code = DoctorCommand.ExitCodeFor(new[]
        {
            new DoctorCheckResult("a", DoctorSeverity.Ok, ""),
            new DoctorCheckResult("b", DoctorSeverity.Warn, ""),
            new DoctorCheckResult("c", DoctorSeverity.Fail, ""),
        });
        Assert.Equal(1, code);
    }

    // ----- Stubs ----------------------------------------------------------

    private sealed class StubJarSource : IJarSource
    {
        public StubJarSource(string cachePath)
        {
            CachePath = cachePath;
            var jarPath = Path.Combine(cachePath, "synthea-with-dependencies.jar");
            File.WriteAllBytes(jarPath, new byte[1024]);
            File.SetLastWriteTimeUtc(jarPath, DateTime.UtcNow.AddDays(-3));
            CachedJar = new FileInfo(jarPath);
        }
        public string CachePath { get; }
        public FileInfo? CachedJar { get; set; }
        public FileInfo? TryFindCachedJar() => CachedJar;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => Task.FromResult(CachedJar ?? throw new InvalidOperationException("no jar"));
    }

    private sealed class StubJavaDetector : IJavaDetector
    {
        public JavaProbeResult Result { get; set; } = new(true, 21, "21.0.5", null);
        public string? LastPath { get; private set; }
        public Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default)
        {
            LastPath = javaPath;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public bool CanWrite { get; set; } = true;
        public string? Error { get; set; }
        public bool TryProbeWrite(string directoryPath, out string? errorMessage)
        {
            errorMessage = CanWrite ? null : (Error ?? "denied");
            return CanWrite;
        }
    }

    private sealed class StubGitHubProbe : IGitHubReachabilityProbe
    {
        public GitHubReachabilityResult Result { get; set; } = new(true, 200, 198, null);
        public Task<GitHubReachabilityResult> PingAsync(TimeSpan timeout, CancellationToken cancelToken)
            => Task.FromResult(Result);
    }

    private sealed class StubDiskSpaceProbe : IDiskSpaceProbe
    {
        public long? FreeBytes { get; set; }
        public long? GetFreeBytes(string path) => FreeBytes;
    }

    // Doctor doesn't spawn any processes today, but DI requires the binding.
    private sealed class NoopProcessRunner : IProcessRunner
    {
        public IProcess Start(System.Diagnostics.ProcessStartInfo psi)
            => throw new InvalidOperationException("doctor should not start processes");
    }
}
