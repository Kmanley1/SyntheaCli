using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.Tests;

public class ProgramHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileInfo _jar;
    private readonly CapturingRunner _runner = new();

    public ProgramHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _jar = new FileInfo(Path.Combine(_tempDir, "synthea.jar"));
        File.WriteAllText(_jar.FullName, "jar");
        Program.Runner = _runner;
        Program.EnsureJarAsyncFunc = (_,_,_) => Task.FromResult(_jar);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
        Program.Runner = new DefaultProcessRunner();
        Program.EnsureJarAsyncFunc = JarManager.EnsureJarAsync;
    }

    [Fact]
    public async Task BuildsProcessStartInfoWithStateAndCity()
    {
        var outDir = Path.Combine(_tempDir, "out1");
        var code = await Program.Main(new[] { "run", "--output", outDir, "--state", "OH", "--city", "Cleveland" });
        Assert.Equal(0, code);

        var psi = _runner.StartInfo!;
        Assert.Equal("java", psi.FileName);
        Assert.Equal(outDir, psi.WorkingDirectory);
        Assert.Equal(new[] { "-jar", _jar.FullName, "OH", "Cleveland" }, psi.ArgumentList);
        Assert.True(Directory.Exists(outDir));
    }

    [Fact]
    public async Task UsesCustomJavaPath()
    {
        var outDir = Path.Combine(_tempDir, "out2");
        var code = await Program.Main(new[] { "run", "--output", outDir, "--java-path", "/usr/bin/custom" });
        Assert.Equal(0, code);
        Assert.Equal("/usr/bin/custom", _runner.StartInfo!.FileName);
    }

    private class CapturingRunner : IProcessRunner
    {
        public ProcessStartInfo? StartInfo;
        public IProcess Start(ProcessStartInfo psi)
        {
            StartInfo = psi;
            return new StubProcess();
        }

        private class StubProcess : IProcess
        {
            public StreamReader StandardOutput { get; } = new StreamReader(new MemoryStream());
            public StreamReader StandardError { get; } = new StreamReader(new MemoryStream());
            public int ExitCode => 0;
            public Task WaitForExitAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
