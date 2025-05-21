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

    [Fact]
    public async Task ForwardsPopulationOption()
    {
        var outDir = Path.Combine(_tempDir, "out3");
        var code = await Program.Main(new[] { "run", "--output", outDir, "-p", "42" });
        Assert.Equal(0, code);
        Assert.Equal(new[] { "-jar", _jar.FullName, "-p", "42" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidPopulationReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out4");
        var code = await Program.Main(new[] { "run", "--output", outDir, "-p", "0" });
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task ForwardsSeedOption()
    {
        var outDir = Path.Combine(_tempDir, "out5");
        var code = await Program.Main(new[] { "run", "--output", outDir, "-s", "123" });
        Assert.Equal(0, code);
        Assert.Equal(new[] { "-jar", _jar.FullName, "-s", "123" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidSeedReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out6");
        var code = await Program.Main(new[] { "run", "--output", outDir, "-s", "oops" });
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task SameSeedProducesSameArguments()
    {
        var outDir1 = Path.Combine(_tempDir, "out7a");
        var code1 = await Program.Main(new[] { "run", "--output", outDir1, "-s", "77" });
        Assert.Equal(0, code1);
        var args1 = _runner.StartInfo!.ArgumentList.ToArray();

        var outDir2 = Path.Combine(_tempDir, "out7b");
        var code2 = await Program.Main(new[] { "run", "--output", outDir2, "-s", "77" });
        Assert.Equal(0, code2);
        var args2 = _runner.StartInfo!.ArgumentList.ToArray();

        Assert.Equal(args1, args2);
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
