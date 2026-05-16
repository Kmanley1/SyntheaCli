using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

// These tests deliberately do NOT redirect Console.Out — Console is
// process-wide and tests run in parallel after Phase 5's
// DisableTestParallelization removal. Behavior is asserted via exit codes
// and filesystem state instead.
public class CacheCommandTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly ServiceProvider _services;

    public CacheCommandTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var sc = new ServiceCollection();
        sc.AddSingleton<IProcessRunner>(new NoopRunner());
        sc.AddSingleton<IJarSource>(new FixedPathJarSource(_cacheDir));
        _services = sc.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        try { Directory.Delete(_cacheDir, true); } catch { }
    }

    [Fact]
    public async Task List_EmptyCache_Succeeds()
    {
        Directory.CreateDirectory(_cacheDir);
        var code = await Program.RunAsync(new[] { "cache", "list" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task List_MissingDirectory_DoesNotThrow()
    {
        // Directory deliberately not created — common first-run state.
        var code = await Program.RunAsync(new[] { "cache", "list" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task List_PopulatedCache_Succeeds()
    {
        Directory.CreateDirectory(_cacheDir);
        File.WriteAllText(Path.Combine(_cacheDir, "synthea-3.4.1-with-dependencies.jar"), new string('x', 1234));
        File.WriteAllText(Path.Combine(_cacheDir, "synthea-3.5.0-with-dependencies.jar"), new string('x', 5678));

        var code = await Program.RunAsync(new[] { "cache", "list" }, _services);
        Assert.Equal(0, code);
        // Files unaffected.
        Assert.Equal(2, Directory.GetFiles(_cacheDir).Length);
    }

    [Fact]
    public async Task Clear_WithYes_DeletesAllFiles()
    {
        Directory.CreateDirectory(_cacheDir);
        File.WriteAllText(Path.Combine(_cacheDir, "a.jar"), "a");
        File.WriteAllText(Path.Combine(_cacheDir, "b.jar"), "b");

        var code = await Program.RunAsync(new[] { "cache", "clear", "--yes" }, _services);
        Assert.Equal(0, code);
        Assert.Empty(Directory.GetFiles(_cacheDir));
    }

    [Fact]
    public async Task Clear_EmptyCache_Succeeds()
    {
        Directory.CreateDirectory(_cacheDir);
        var code = await Program.RunAsync(new[] { "cache", "clear", "--yes" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Clear_MissingDirectory_Succeeds()
    {
        // Directory deliberately not created.
        var code = await Program.RunAsync(new[] { "cache", "clear", "--yes" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ShortAlias_Y_AlsoSkipsPrompt()
    {
        Directory.CreateDirectory(_cacheDir);
        File.WriteAllText(Path.Combine(_cacheDir, "x.jar"), "x");

        var code = await Program.RunAsync(new[] { "cache", "clear", "-y" }, _services);
        Assert.Equal(0, code);
        Assert.Empty(Directory.GetFiles(_cacheDir));
    }

    private sealed class FixedPathJarSource : IJarSource
    {
        private readonly string _cachePath;
        public FixedPathJarSource(string cachePath) => _cachePath = cachePath;
        public string CachePath => _cachePath;
        public FileInfo? TryFindCachedJar() => null;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => throw new NotSupportedException();
    }

    private sealed class NoopRunner : IProcessRunner
    {
        public IProcess Start(System.Diagnostics.ProcessStartInfo psi)
            => throw new NotSupportedException();
    }
}
