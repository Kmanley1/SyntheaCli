using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class ProgramHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileInfo _jar;
    private readonly CapturingRunner _runner = new();
    private readonly ServiceProvider _services;

    public ProgramHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _jar = new FileInfo(Path.Combine(_tempDir, "synthea.jar"));
        File.WriteAllText(_jar.FullName, "jar");

        var sc = new ServiceCollection();
        sc.AddSingleton<IProcessRunner>(_runner);
        sc.AddSingleton<IJarSource>(new StubJarSource(_jar));
        _services = sc.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private Task<int> Run(params string[] args) => Program.RunAsync(args, _services);

    [Fact]
    public async Task BuildsProcessStartInfoWithStateAndCity()
    {
        var outDir = Path.Combine(_tempDir, "out1");
        // 2-letter code on input; CLI normalizes to "Ohio" before passthru
        // because Synthea's geography data is keyed by full state name.
        var code = await Run("run", "--output", outDir, "--state", "OH", "--city", "Cleveland");
        Assert.Equal(0, code);

        var psi = _runner.StartInfo!;
        Assert.Equal("java", psi.FileName);
        Assert.Equal(outDir, psi.WorkingDirectory);
        Assert.Equal(new[] { "-jar", _jar.FullName, "Ohio", "Cleveland" }, psi.ArgumentList);
        Assert.True(Directory.Exists(outDir));
    }

    [Fact]
    public async Task StateFullName_PassesThroughUnchanged()
    {
        var outDir = Path.Combine(_tempDir, "out-state-fullname");
        var code = await Run("run", "--output", outDir, "--state", "Massachusetts");
        Assert.Equal(0, code);

        Assert.Equal(new[] { "-jar", _jar.FullName, "Massachusetts" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task StateMultiWordFullName_PassesThroughUnchanged()
    {
        var outDir = Path.Combine(_tempDir, "out-state-multiword");
        var code = await Run("run", "--output", outDir, "--state", "New Hampshire");
        Assert.Equal(0, code);

        Assert.Equal(new[] { "-jar", _jar.FullName, "New Hampshire" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task StateLowercaseCode_NormalizedToFullName()
    {
        var outDir = Path.Combine(_tempDir, "out-state-lower");
        var code = await Run("run", "--output", outDir, "--state", "tx");
        Assert.Equal(0, code);

        Assert.Equal(new[] { "-jar", _jar.FullName, "Texas" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidStateCodeReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out-state-bad");
        var code = await Run("run", "--output", outDir, "--state", "ZZ");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task UsesCustomJavaPath()
    {
        var outDir = Path.Combine(_tempDir, "out2");
        var code = await Run("run", "--output", outDir, "--java-path", "/usr/bin/custom");
        Assert.Equal(0, code);
        Assert.Equal("/usr/bin/custom", _runner.StartInfo!.FileName);
    }

    [Fact]
    public async Task ForwardsPopulationOption()
    {
        var outDir = Path.Combine(_tempDir, "out3");
        var code = await Run("run", "--output", outDir, "-p", "42");
        Assert.Equal(0, code);
        Assert.Equal(new[] { "-jar", _jar.FullName, "-p", "42" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidPopulationReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out4");
        var code = await Run("run", "--output", outDir, "-p", "0");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task ForwardsSeedOption()
    {
        var outDir = Path.Combine(_tempDir, "out5");
        var code = await Run("run", "--output", outDir, "-s", "123");
        Assert.Equal(0, code);
        Assert.Equal(new[] { "-jar", _jar.FullName, "-s", "123" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidSeedReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out6");
        var code = await Run("run", "--output", outDir, "-s", "oops");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task SameSeedProducesSameArguments()
    {
        var outDir1 = Path.Combine(_tempDir, "out7a");
        var code1 = await Run("run", "--output", outDir1, "-s", "77");
        Assert.Equal(0, code1);
        var args1 = _runner.StartInfo!.ArgumentList.ToArray();

        var outDir2 = Path.Combine(_tempDir, "out7b");
        var code2 = await Run("run", "--output", outDir2, "-s", "77");
        Assert.Equal(0, code2);
        var args2 = _runner.StartInfo!.ArgumentList.ToArray();

        Assert.Equal(args1, args2);
    }

    [Fact]
    public async Task ForwardsGenderOption()
    {
        var outDir = Path.Combine(_tempDir, "out8");
        var code = await Run("run", "--output", outDir, "--gender", "M");
        Assert.Equal(0, code);
        Assert.Contains("--gender", _runner.StartInfo!.ArgumentList);
        Assert.Contains("M", _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidGenderReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out9");
        var code = await Run("run", "--output", outDir, "--gender", "X");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task ForwardsAgeRangeOption()
    {
        var outDir = Path.Combine(_tempDir, "out10");
        var code = await Run("run", "--output", outDir, "--age-range", "10-20");
        Assert.Equal(0, code);
        Assert.Contains("--age-range", _runner.StartInfo!.ArgumentList);
        Assert.Contains("10-20", _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidAgeRangeReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out11");
        var code = await Run("run", "--output", outDir, "--age-range", "a-b");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task ForwardsModuleDirOption()
    {
        var modDir = Path.Combine(_tempDir, "mods");
        Directory.CreateDirectory(modDir);
        var outDir = Path.Combine(_tempDir, "out12");
        var code = await Run("run", "--output", outDir, "--module-dir", modDir);
        Assert.Equal(0, code);
        Assert.Contains("--module-dir", _runner.StartInfo!.ArgumentList);
        Assert.Contains(modDir, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task InvalidModuleDirReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out13");
        var code = await Run("run", "--output", outDir, "--module-dir", Path.Combine(_tempDir, "nope"));
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task SnapshotArguments_ValidInput()
    {
        var init = Path.Combine(_tempDir, "snap_in.json");
        File.WriteAllText(init, "snap");
        var upd = Path.Combine(_tempDir, "snap_out.json");
        var outDir = Path.Combine(_tempDir, "out17");
        var code = await Run(
            "run", "--output", outDir,
            "--initial-snapshot", init,
            "--updated-snapshot", upd,
            "--days-forward", "15");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-i", list);
        Assert.Contains(init, list);
        Assert.Contains("-u", list);
        Assert.Contains(upd, list);
        Assert.Contains("-t", list);
        Assert.Contains("15", list);
    }

    [Fact]
    public async Task SnapshotArguments_InvalidInput()
    {
        var outDir = Path.Combine(_tempDir, "out18");
        var code1 = await Run("run", "--output", outDir, "--initial-snapshot", Path.Combine(_tempDir, "missing.json"));
        Assert.NotEqual(0, code1);

        var init = Path.Combine(_tempDir, "snap_in2.json");
        File.WriteAllText(init, "snap");
        var code2 = await Run("run", "--output", outDir, "--initial-snapshot", init, "--days-forward", "0");
        Assert.NotEqual(0, code2);
    }

    [Fact]
    public async Task FormatArgument_ValidFormat()
    {
        var outDir = Path.Combine(_tempDir, "out19");
        var code = await Run("run", "--output", outDir, "--format", "FHIR", "--format", "CSV");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("--exporter.fhir.export=true", list);
        Assert.Contains("--exporter.csv.export=true", list);
        Assert.Contains("--exporter.ccda.export=false", list);
    }

    [Fact]
    public async Task FormatArgument_InvalidFormat()
    {
        var outDir = Path.Combine(_tempDir, "out20");
        var code = await Run("run", "--output", outDir, "--format", "BAD");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task ForwardsModuleOption()
    {
        var outDir = Path.Combine(_tempDir, "out14");
        var code = await Run("run", "--output", outDir, "--module", "flu", "--module", "covid");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Equal(new[] { "-jar", _jar.FullName, "--module", "flu", "--module", "covid" }, list);
    }

    [Fact]
    public async Task CityWithoutStateReturnsError()
    {
        var outDir = Path.Combine(_tempDir, "out15");
        var code = await Run("run", "--output", outDir, "--city", "Austin");
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Theory]
    [InlineData("X")]      // single letter — neither a 2-letter code nor a plausible state name
    [InlineData("X1")]     // 2-char but not all letters
    [InlineData("ZZ")]     // 2-letter shape but not a real USPS code
    [InlineData("99")]     // 2-char digits — fails the 2-letter and the >=3 checks
    public async Task InvalidStateFormatReturnsError(string badState)
    {
        var outDir = Path.Combine(_tempDir, Path.GetRandomFileName());
        var code = await Run("run", "--output", outDir, "--state", badState);
        Assert.NotEqual(0, code);
    }

    [Theory]
    [InlineData("DC")]     // District of Columbia — rejected by the old US-only allowlist
    [InlineData("PR")]     // Puerto Rico — same
    public async Task TerritoryStateCodesAreAccepted(string state)
    {
        var outDir = Path.Combine(_tempDir, Path.GetRandomFileName());
        var code = await Run("run", "--output", outDir, "--state", state);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ConfigArgument_ValidFile()
    {
        var cfg = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(cfg, "{}");
        var outDir = Path.Combine(_tempDir, "out21");
        var code = await Run("run", "--output", outDir, "--config", cfg);
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-c", list);
        Assert.Contains(cfg, list);
    }

    [Fact]
    public async Task ConfigArgument_InvalidFile()
    {
        var outDir = Path.Combine(_tempDir, "out22");
        var code = await Run("run", "--output", outDir, "--config", Path.Combine(_tempDir, "missing.json"));
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task ZipArgument_ValidZip()
    {
        var outDir = Path.Combine(_tempDir, "out23");
        // OH normalizes to Ohio for the passthru (Synthea wants full names).
        var code = await Run("run", "--output", outDir, "--state", "OH", "--zip", "44101");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Equal(new[] { "-jar", _jar.FullName, "Ohio", "44101" }, list);
    }

    [Fact]
    public async Task ZipArgument_InvalidZip()
    {
        var outDir = Path.Combine(_tempDir, "out24");
        var code = await Run("run", "--output", outDir, "--state", "OH", "--zip", "bad");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public async Task FhirVersionArgument_Valid()
    {
        var outDir = Path.Combine(_tempDir, "out25");
        var code = await Run("run", "--output", outDir, "--fhir-version", "R4");
        Assert.Equal(0, code);
        Assert.Contains("--exporter.fhir.version=R4", _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task FhirVersionArgument_Invalid()
    {
        var outDir = Path.Combine(_tempDir, "out26");
        var code = await Run("run", "--output", outDir, "--fhir-version", "XYZ");
        Assert.NotEqual(0, code);
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
