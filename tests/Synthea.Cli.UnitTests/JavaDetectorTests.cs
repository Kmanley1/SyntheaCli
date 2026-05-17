using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class JavaDetectorTests
{
    [Fact]
    public void JavaDetector_Parses_OpenJDK17()
    {
        const string output = "openjdk version \"17.0.10\" 2024-01-16 LTS\n" +
                              "OpenJDK Runtime Environment Temurin-17.0.10+7 (build 17.0.10+7-LTS)\n";
        var r = JavaDetector.Parse(output);
        Assert.True(r.Found);
        Assert.Equal(17, r.MajorVersion);
        Assert.Equal("17.0.10", r.RawVersionString);
        Assert.Null(r.ErrorMessage);
    }

    [Fact]
    public void JavaDetector_Parses_OracleJDK21()
    {
        const string output = "java version \"21.0.5\" 2024-10-15 LTS\n" +
                              "Java(TM) SE Runtime Environment (build 21.0.5+9-LTS-239)\n";
        var r = JavaDetector.Parse(output);
        Assert.True(r.Found);
        Assert.Equal(21, r.MajorVersion);
        Assert.Equal("21.0.5", r.RawVersionString);
    }

    [Fact]
    public void JavaDetector_Parses_OldJava11()
    {
        const string output = "openjdk version \"11.0.20\" 2023-07-18\n" +
                              "OpenJDK Runtime Environment Temurin-11.0.20+8\n";
        var r = JavaDetector.Parse(output);
        Assert.True(r.Found);
        Assert.Equal(11, r.MajorVersion);
    }

    [Fact]
    public void JavaDetector_Parses_LegacyJava8()
    {
        // Old "1.8.0_201" form — major version is the integer after the "1.".
        const string output = "java version \"1.8.0_201\"\n" +
                              "Java(TM) SE Runtime Environment (build 1.8.0_201-b09)\n";
        var r = JavaDetector.Parse(output);
        Assert.True(r.Found);
        Assert.Equal(8, r.MajorVersion);
    }

    [Fact]
    public void JavaDetector_Parses_GarbledOutput_ReportsNotFound()
    {
        var r = JavaDetector.Parse("totally not java output");
        Assert.False(r.Found);
        Assert.Null(r.MajorVersion);
        Assert.NotNull(r.ErrorMessage);
    }

    [Fact]
    public void JavaDetector_Parses_EmptyOutput_ReportsNotFound()
    {
        var r = JavaDetector.Parse(string.Empty);
        Assert.False(r.Found);
    }

    [Fact]
    public async Task JavaDetector_HandlesMissingJava()
    {
        var runner = new ThrowingRunner();
        var detector = new JavaDetector(runner);
        var r = await detector.ProbeAsync("definitely-not-a-real-binary-9c4e1a");
        Assert.False(r.Found);
        Assert.NotNull(r.ErrorMessage);
    }

    [Fact]
    public async Task JavaDetector_ProbeAsync_DelegatesToRunner_ParsesStderr()
    {
        var runner = new StubRunner(stderr: "openjdk version \"21.0.5\" 2024-10-15 LTS\n", stdout: "");
        var detector = new JavaDetector(runner);
        var r = await detector.ProbeAsync("java");
        Assert.True(r.Found);
        Assert.Equal(21, r.MajorVersion);
    }

    [Fact]
    public async Task JavaDetector_ProbeAsync_EmptyPath_ReturnsNotFound()
    {
        var detector = new JavaDetector(new ThrowingRunner());
        var r = await detector.ProbeAsync("");
        Assert.False(r.Found);
        Assert.Equal("Java path was empty.", r.ErrorMessage);
    }

    private sealed class ThrowingRunner : IProcessRunner
    {
        public IProcess Start(ProcessStartInfo psi) => throw new FileNotFoundException("nope");
    }

    private sealed class StubRunner : IProcessRunner
    {
        private readonly string _stderr;
        private readonly string _stdout;
        public StubRunner(string stderr, string stdout) { _stderr = stderr; _stdout = stdout; }
        public IProcess Start(ProcessStartInfo psi) => new StubProcess(_stdout, _stderr);

        private sealed class StubProcess : IProcess
        {
            public StubProcess(string stdout, string stderr)
            {
                StandardOutput = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stdout)));
                StandardError = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(stderr)));
            }
            public StreamReader StandardOutput { get; }
            public StreamReader StandardError { get; }
            public int ExitCode => 0;
            public Task WaitForExitAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
