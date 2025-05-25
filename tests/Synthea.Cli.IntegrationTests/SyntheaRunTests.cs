using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
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

        Program.Runner = new FakeRunner(outputDir);
        Program.EnsureJarAsyncFunc = (_, _, _) => Task.FromResult(new FileInfo(Path.Combine(_workDir, "dummy.jar")));

        var exit = await Program.Main(new[] { "run", "--output", _workDir, "--population", "1" });

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
            public void Dispose() {}
        }
    }
}
