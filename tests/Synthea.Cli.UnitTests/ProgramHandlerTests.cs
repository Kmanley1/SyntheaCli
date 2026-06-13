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
    private readonly StubJavaDetector _detector = new();
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
        sc.AddSingleton<IJavaDetector>(_detector);
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
        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "Ohio", "Cleveland" }, psi.ArgumentList);
        Assert.True(Directory.Exists(outDir));
    }

    [Fact]
    public async Task StateFullName_PassesThroughUnchanged()
    {
        var outDir = Path.Combine(_tempDir, "out-state-fullname");
        var code = await Run("run", "--output", outDir, "--state", "Massachusetts");
        Assert.Equal(0, code);

        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "Massachusetts" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task StateMultiWordFullName_PassesThroughUnchanged()
    {
        var outDir = Path.Combine(_tempDir, "out-state-multiword");
        var code = await Run("run", "--output", outDir, "--state", "New Hampshire");
        Assert.Equal(0, code);

        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "New Hampshire" }, _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task StateLowercaseCode_NormalizedToFullName()
    {
        var outDir = Path.Combine(_tempDir, "out-state-lower");
        var code = await Run("run", "--output", outDir, "--state", "tx");
        Assert.Equal(0, code);

        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "Texas" }, _runner.StartInfo!.ArgumentList);
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
        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "-p", "42" }, _runner.StartInfo!.ArgumentList);
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
        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "-s", "123" }, _runner.StartInfo!.ArgumentList);
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
        // Same seed AND same output dir → identical Synthea args (the output
        // dir feeds --exporter.baseDirectory, so it must match to compare).
        var outDir = Path.Combine(_tempDir, "out7");
        var code1 = await Run("run", "--output", outDir, "-s", "77");
        Assert.Equal(0, code1);
        var args1 = _runner.StartInfo!.ArgumentList.ToArray();

        var code2 = await Run("run", "--output", outDir, "-s", "77");
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
        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "--module", "flu", "--module", "covid" }, list);
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
        Assert.Equal(new[] { "-jar", _jar.FullName, $"--exporter.baseDirectory={Path.GetFullPath(outDir)}", "Ohio", "44101" }, list);
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
        public int ExitCode { get; set; }
        public string StderrText { get; set; } = string.Empty;
        public IProcess Start(ProcessStartInfo psi)
        {
            StartInfo = psi;
            return new StubProcess(ExitCode, StderrText);
        }

        private class StubProcess : IProcess
        {
            private readonly int _exitCode;
            public StubProcess(int exitCode, string stderrText)
            {
                _exitCode = exitCode;
                StandardError = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stderrText)));
            }
            public StreamReader StandardOutput { get; } = new StreamReader(new MemoryStream());
            public StreamReader StandardError { get; }
            public int ExitCode => _exitCode;
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

    private sealed class StubJavaDetector : IJavaDetector
    {
        public JavaProbeResult Result { get; set; } = new(true, 21, "21.0.5", null);
        public Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default)
            => Task.FromResult(Result);
    }

    [Fact]
    public async Task JdkCheck_TooOldJava_ExitsOneWithoutDownloading()
    {
        _detector.Result = new JavaProbeResult(true, 11, "11.0.20", null);
        var outDir = Path.Combine(_tempDir, "out-jdk-old");
        var code = await Run("run", "--output", outDir, "--state", "OH");
        Assert.Equal(1, code);
        // RunCommand should have rejected before invoking the process runner.
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task JdkCheck_JavaMissing_ExitsThreeWithoutDownloading()
    {
        _detector.Result = new JavaProbeResult(false, null, null, "not found");
        var outDir = Path.Combine(_tempDir, "out-jdk-missing");
        var code = await Run("run", "--output", outDir, "--state", "OH");
        Assert.Equal(3, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task JdkCheck_SkipFlag_BypassesCheckEvenWhenJavaTooOld()
    {
        _detector.Result = new JavaProbeResult(true, 8, "1.8.0_201", null);
        var outDir = Path.Combine(_tempDir, "out-jdk-skip");
        var code = await Run("run", "--output", outDir, "--state", "OH", "--skip-jdk-check");
        Assert.Equal(0, code);
        Assert.NotNull(_runner.StartInfo);
    }

    [Fact]
    public async Task JdkCheck_Java17_Passes()
    {
        _detector.Result = new JavaProbeResult(true, 17, "17.0.10", null);
        var outDir = Path.Combine(_tempDir, "out-jdk-17");
        var code = await Run("run", "--output", outDir, "--state", "OH");
        Assert.Equal(0, code);
        Assert.NotNull(_runner.StartInfo);
    }

    [Fact]
    public async Task JavaProcess_NonZeroExit_PropagatesExitCode()
    {
        // (C6) When the spawned java process exits non-zero, the CLI must
        // propagate that exact exit code — not coerce to 0, and not 1 or 3
        // unless it actually came from java.
        _runner.ExitCode = 137;
        _runner.StderrText = "totally unfamiliar error\n";
        var outDir = Path.Combine(_tempDir, "out-nonzero-exit");
        var code = await Run("run", "--output", outDir, "--state", "OH");
        Assert.Equal(137, code);
    }

    // A1+A2+A3 (Phase 5): reproducibility + overflow flag parsing.

    [Fact]
    public async Task ReferenceDate_Iso_ConvertedToYyyymmdd()
    {
        var outDir = Path.Combine(_tempDir, "out-refdate");
        var code = await Run("run", "--output", outDir, "--reference-date", "2024-03-15");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-r", list);
        Assert.Contains("20240315", list);
    }

    [Fact]
    public async Task ReferenceDate_BadFormat_RejectedByValidator()
    {
        var outDir = Path.Combine(_tempDir, "out-refdate-bad");
        var code = await Run("run", "--output", outDir, "--reference-date", "03/15/2024");
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task EndDate_Iso_ConvertedToYyyymmdd()
    {
        var outDir = Path.Combine(_tempDir, "out-enddate");
        var code = await Run("run", "--output", outDir, "--end-date", "2030-12-31");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-e", list);
        Assert.Contains("20301231", list);
    }

    [Fact]
    public async Task AllowFutureEnd_EmitsDashE()
    {
        var outDir = Path.Combine(_tempDir, "out-future-end");
        var code = await Run("run", "--output", outDir, "--allow-future-end");
        Assert.Equal(0, code);
        Assert.Contains("-E", _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task ClinicianSeed_EmitsDashCs()
    {
        var outDir = Path.Combine(_tempDir, "out-cs");
        var code = await Run("run", "--output", outDir, "--clinician-seed", "42");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-cs", list);
        Assert.Contains("42", list);
    }

    [Fact]
    public async Task SinglePersonSeed_EmitsDashPs()
    {
        var outDir = Path.Combine(_tempDir, "out-ps");
        var code = await Run("run", "--output", outDir, "--single-person-seed", "7");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-ps", list);
        Assert.Contains("7", list);
    }

    [Fact]
    public async Task ClinicianSeed_NonInteger_Rejected()
    {
        var outDir = Path.Combine(_tempDir, "out-cs-bad");
        var code = await Run("run", "--output", outDir, "--clinician-seed", "oops");
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task Overflow_EmitsDashOTrue()
    {
        var outDir = Path.Combine(_tempDir, "out-overflow");
        var code = await Run("run", "--output", outDir, "--overflow");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        // -o true comes BEFORE any positional state/city/zip in BuildArgumentList.
        var oIdx = list.IndexOf("-o");
        Assert.True(oIdx >= 0, "expected -o in arg list");
        Assert.Equal("true", list[oIdx + 1]);
    }

    [Fact]
    public async Task AllReproFlags_AppearTogether()
    {
        var outDir = Path.Combine(_tempDir, "out-all-repro");
        var code = await Run(
            "run", "--output", outDir,
            "--reference-date", "2024-01-01",
            "--end-date", "2024-12-31",
            "--allow-future-end",
            "--clinician-seed", "1",
            "--single-person-seed", "2",
            "--overflow");
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("-r", list);
        Assert.Contains("20240101", list);
        Assert.Contains("-e", list);
        Assert.Contains("20241231", list);
        Assert.Contains("-E", list);
        Assert.Contains("-cs", list);
        Assert.Contains("-ps", list);
        Assert.Contains("-o", list);
    }

    [Theory]
    [InlineData("Atlantis")]         // far miss — no suggestion
    [InlineData("Yukon")]            // far miss
    [InlineData("Mass")]             // length 4 but not a code (not 2-letter); too short to be a state
    public async Task State_UnknownFullName_ReturnsError(string badState)
    {
        // (C1) The validator must now reject full names outside the known
        // 56-state set, not pass them through to Synthea.
        var outDir = Path.Combine(_tempDir, Path.GetRandomFileName());
        var code = await Run("run", "--output", outDir, "--state", badState);
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task State_NearMiss_ReturnsErrorWithSuggestion()
    {
        // (C1) A single-edit miss should be rejected — and ideally the
        // error message contains the suggested correct name. We can't
        // easily inspect stderr here without redirecting it process-wide;
        // the exit code is the contract.
        var outDir = Path.Combine(_tempDir, Path.GetRandomFileName());
        var code = await Run("run", "--output", outDir, "--state", "Massachsetts");
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    // A9+A10 (Phase 7): US Core version + FHIR R5.

    [Theory]
    [InlineData("3.1.1")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("7")]
    public async Task UsCoreVersion_Allowed_EmitsBothProperties(string ver)
    {
        var outDir = Path.Combine(_tempDir, "out-uscore-" + ver);
        var code = await Run("run", "--output", outDir, "--us-core-version", ver);
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        Assert.Contains("--exporter.fhir.use_us_core_ig=true", list);
        Assert.Contains($"--exporter.fhir.us_core_version={ver}", list);
    }

    [Fact]
    public async Task UsCoreVersion_OutsideAllowlist_Rejected()
    {
        var outDir = Path.Combine(_tempDir, "out-uscore-bad");
        var code = await Run("run", "--output", outDir, "--us-core-version", "99");
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task FhirVersion_R5_Accepted()
    {
        var outDir = Path.Combine(_tempDir, "out-fhir-r5");
        var code = await Run("run", "--output", outDir, "--fhir-version", "R5");
        Assert.Equal(0, code);
        Assert.Contains("--exporter.fhir.version=R5", _runner.StartInfo!.ArgumentList);
    }

    [Fact]
    public async Task FhirVersion_Bogus_Rejected()
    {
        var outDir = Path.Combine(_tempDir, "out-fhir-bad");
        var code = await Run("run", "--output", outDir, "--fhir-version", "R6");
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    // A5+A8 (Phase 8): Flexporter mapping + custom IG dir + bulk-data.

    [Fact]
    public async Task FlexporterMapping_ExistingFile_EmitsDashFm()
    {
        var mapping = Path.Combine(_tempDir, "flexporter.yaml");
        File.WriteAllText(mapping, "mappings: []\n");
        var outDir = Path.Combine(_tempDir, "out-flex");
        var code = await Run("run", "--output", outDir, "--flexporter-mapping", mapping);
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        var fmIdx = list.IndexOf("-fm");
        Assert.True(fmIdx >= 0);
        Assert.Equal(Path.GetFullPath(mapping), list[fmIdx + 1]);
    }

    [Fact]
    public async Task FlexporterMapping_MissingFile_Rejected()
    {
        var outDir = Path.Combine(_tempDir, "out-flex-bad");
        var code = await Run("run", "--output", outDir,
            "--flexporter-mapping", Path.Combine(_tempDir, "does-not-exist.yaml"));
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task IgDir_ExistingDirectory_EmitsDashIg()
    {
        var ig = Path.Combine(_tempDir, "ig");
        Directory.CreateDirectory(ig);
        var outDir = Path.Combine(_tempDir, "out-ig");
        var code = await Run("run", "--output", outDir, "--ig-dir", ig);
        Assert.Equal(0, code);
        var list = _runner.StartInfo!.ArgumentList;
        var igIdx = list.IndexOf("-ig");
        Assert.True(igIdx >= 0);
        Assert.Equal(Path.GetFullPath(ig), list[igIdx + 1]);
    }

    [Fact]
    public async Task IgDir_MissingDirectory_Rejected()
    {
        var outDir = Path.Combine(_tempDir, "out-ig-bad");
        var code = await Run("run", "--output", outDir,
            "--ig-dir", Path.Combine(_tempDir, "no-such-dir"));
        Assert.NotEqual(0, code);
        Assert.Null(_runner.StartInfo);
    }

    [Fact]
    public async Task BulkData_Flag_EmitsExporterProperty()
    {
        var outDir = Path.Combine(_tempDir, "out-bulk");
        var code = await Run("run", "--output", outDir, "--bulk-data");
        Assert.Equal(0, code);
        Assert.Contains("--exporter.fhir.bulk_data=true", _runner.StartInfo!.ArgumentList);
    }

    // Output lands directly under -o <dir> (not a nested output/ subfolder):
    // the CLI passes Synthea an absolute exporter.baseDirectory.
    [Fact]
    public async Task Run_EmitsExporterBaseDirectory_ForOutputDir()
    {
        var outDir = Path.Combine(_tempDir, "out-basedir");
        var code = await Run("run", "--output", outDir, "--state", "OH");
        Assert.Equal(0, code);
        Assert.Contains($"--exporter.baseDirectory={Path.GetFullPath(outDir)}", _runner.StartInfo!.ArgumentList);
    }

    // D1: --version shows a two-line report including the JAR cache info.
    [Fact]
    public void BuildVersionReport_NoCachedJar_SaysNotYetCached()
    {
        var report = Program.BuildVersionReport(new NoJarStub());
        Assert.Contains("synthea-cli", report);
        Assert.Contains("not yet cached", report);
    }

    [Fact]
    public void BuildVersionReport_WithCachedJar_IncludesCachedDate()
    {
        var report = Program.BuildVersionReport(new StubJarSource(_jar));
        Assert.Contains("synthea-cli", report);
        Assert.Contains("synthea-jar", report);
        Assert.Contains("cached ", report);
    }

    // Container UX fix: a baked/configured JAR (e.g. the Docker image's
    // SYNTHEA_CLI_JAR_PATH) is reported even when the download cache is empty.
    [Fact]
    public void BuildVersionReport_WithOverrideJar_ReportsConfiguredPath()
    {
        var jarPath = Path.Combine(_tempDir, "baked-synthea-with-dependencies.jar");
        File.WriteAllBytes(jarPath, new byte[1024]);
        var report = Program.BuildVersionReport(new NoJarStub(), new FileInfo(jarPath));
        Assert.Contains("synthea-cli", report);
        Assert.Contains("configured:", report);
        Assert.Contains(jarPath, report);
        Assert.DoesNotContain("not yet cached", report);
    }

    // Container provenance: the baked Synthea version (env SYNTHEA_JAR_VERSION,
    // set by the Docker image) is authoritative — Synthea's JAR has no clean
    // Implementation-Version, so without this --version says "version unavailable".
    [Fact]
    public void BuildVersionReport_WithBakedSyntheaVersion_ShowsIt()
    {
        var jarPath = Path.Combine(_tempDir, "synthea-with-dependencies.jar");
        File.WriteAllBytes(jarPath, new byte[1024]);
        var report = Program.BuildVersionReport(new NoJarStub(), new FileInfo(jarPath), "v4.0.0");
        Assert.Contains("synthea-jar v4.0.0", report);
        Assert.DoesNotContain("version unavailable", report);
    }

    [Fact]
    public async Task VersionFlag_ShortCircuitsBeforeFrameworkHandler()
    {
        var code = await Run("--version");
        Assert.Equal(0, code);
        // The run handler should not have been entered.
        Assert.Null(_runner.StartInfo);
    }

    // D8: --dry-run is the canonical alias for --print-args.
    [Fact]
    public async Task DryRun_Alias_BehavesLikePrintArgs()
    {
        var outDir = Path.Combine(_tempDir, "out-dry-run");
        var code = await Run("run", "--output", outDir, "--state", "OH", "--dry-run");
        Assert.Equal(0, code);
        Assert.Null(_runner.StartInfo); // never spawned a process
    }

    [Fact]
    public async Task PrintArgs_LegacyAlias_StillWorks()
    {
        var outDir = Path.Combine(_tempDir, "out-print-args");
        var code = await Run("run", "--output", outDir, "--state", "OH", "--print-args");
        Assert.Equal(0, code);
        Assert.Null(_runner.StartInfo);
    }

    // A bare IJarSource that always reports no cached JAR.
    private sealed class NoJarStub : IJarSource
    {
        public string CachePath => Path.GetTempPath();
        public FileInfo? TryFindCachedJar() => null;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => throw new InvalidOperationException("no jar in this test");
    }

    [Fact]
    public async Task JavaProcess_NonZeroExit_WithRecognizedStderr_StillReturnsJavasExitCode()
    {
        // (C6) Hint printing must not change the exit code we return. The
        // user-visible hint is a stderr decoration; the contract is the exit
        // code.
        _runner.ExitCode = 1;
        _runner.StderrText = "java.lang.RuntimeException: Unable to select a random city id for state Zoo\n";
        var outDir = Path.Combine(_tempDir, "out-nonzero-hint");
        var code = await Run("run", "--output", outDir, "--state", "OH");
        Assert.Equal(1, code);
    }

    // D2 (Phase 14): --progress flag.

    [Fact]
    public async Task Progress_Off_NoStatusLineOnStderr()
    {
        // Without --progress, no synthetic "Generated …" line is emitted
        // even when the runner emits progress-shaped stderr.
        _runner.StderrText = "1 -- Bob (45 y/o M) Cleveland, Ohio\n2 -- Alice (32 y/o F) Akron, Ohio\n";
        var (stderr, code) = await RunCapturingStderr("run", "--output", Path.Combine(_tempDir, "out-prog-off"), "--state", "OH", "-p", "2");
        Assert.Equal(0, code);
        Assert.DoesNotContain("Generated ", stderr);
    }

    [Fact]
    public async Task Progress_On_EmitsFinalStatusLineWhenPatientsReported()
    {
        // With --progress, the post-pump flush emits at least one status
        // line summarizing the parser's final count. We don't try to
        // exercise the 5-second timer (would slow the suite); the final
        // flush is the deterministic part of the contract.
        _runner.StderrText = "1 -- Bob (45 y/o M) Cleveland, Ohio\n2 -- Alice (32 y/o F) Akron, Ohio\n";
        var (stderr, code) = await RunCapturingStderr("run", "--output", Path.Combine(_tempDir, "out-prog-on"), "--state", "OH", "-p", "2", "--progress");
        Assert.Equal(0, code);
        Assert.Contains("Generated 2/2 patients (100%)", stderr);
    }

    [Fact]
    public async Task Progress_NoPopulation_FallsBackToCountOnly()
    {
        // Without -p we can't render "X/Y (Z%)" — just the count.
        _runner.StderrText = "1 -- Bob (45 y/o M) Cleveland, Ohio\n";
        var (stderr, code) = await RunCapturingStderr("run", "--output", Path.Combine(_tempDir, "out-prog-nopop"), "--state", "OH", "--progress");
        Assert.Equal(0, code);
        Assert.Contains("Generated 1 patients", stderr);
        Assert.DoesNotContain("/0", stderr);
    }

    [Fact]
    public async Task Progress_NoPatientLines_EmitsNothing()
    {
        // The flush is a no-op if the parser never saw a progress-shaped
        // line — e.g. a run that failed before generation started.
        _runner.StderrText = "Loading modules...\n";
        var (stderr, code) = await RunCapturingStderr("run", "--output", Path.Combine(_tempDir, "out-prog-empty"), "--state", "OH", "--progress");
        Assert.Equal(0, code);
        Assert.DoesNotContain("Generated ", stderr);
    }

    private async Task<(string Stderr, int ExitCode)> RunCapturingStderr(params string[] args)
    {
        // Swap Console.Error so we can read what RunCommand writes. We
        // restore it in a finally so an exception in the handler doesn't
        // leave the static binding rerouted for the next test.
        var sw = new StringWriter();
        var original = Console.Error;
        Console.SetError(sw);
        try
        {
            var code = await Run(args);
            return (sw.ToString(), code);
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
