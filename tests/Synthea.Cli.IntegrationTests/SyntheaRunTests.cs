using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.IntegrationTests;

[Trait("Category", "Integration")]
public class SyntheaRunTests : IDisposable
{
    private readonly string _workDir;

    public SyntheaRunTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch { }
    }

    [Fact]
    public async Task Synthea_Run_Generates_Output()
    {
        var outputDir = Path.Combine(_workDir, "output");
        Directory.CreateDirectory(outputDir);

        var jar = new FileInfo(Path.Combine(_workDir, "dummy.jar"));
        var sc = new ServiceCollection();
        sc.AddSingleton<IProcessRunner>(new FakeRunner(outputDir));
        sc.AddSingleton<IJarSource>(new StubJarSource(jar));
        await using var services = sc.BuildServiceProvider();

        var exit = await Program.RunAsync(new[] { "run", "--output", _workDir, "--population", "1" }, services);

        Assert.Equal(0, exit);
        Assert.True(Directory.Exists(outputDir));
        Assert.NotEmpty(Directory.GetFiles(outputDir));
    }

    private sealed class FakeRunner : IProcessRunner
    {
        private readonly string _outDir;
        public FakeRunner(string outDir) => _outDir = outDir;
        public IProcess Start(ProcessStartInfo psi) => new FakeProcess(_outDir);

        private sealed class FakeProcess : IProcess
        {
            public FakeProcess(string outDir)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "patient.json"), "{}");
            }
            public StreamReader StandardOutput { get; } = new StreamReader(new MemoryStream());
            public StreamReader StandardError { get; } = new StreamReader(new MemoryStream());
            public Task WaitForExitAsync() => Task.CompletedTask;
            public int ExitCode => 0;
            public void Dispose() { }
        }
    }

    private sealed class StubJarSource : IJarSource
    {
        private readonly FileInfo _jar;
        public StubJarSource(FileInfo jar) => _jar = jar;
        public string CachePath => _jar.DirectoryName ?? Path.GetTempPath();
        public FileInfo? TryFindCachedJar() => _jar;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => Task.FromResult(_jar);
    }
}
