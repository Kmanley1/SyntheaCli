using System;
using System.IO;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class ProgramRefactorTests
{
    // Regression guard for notes §5.4: in System.CommandLine 2.0 GA the
    // validator delegate is Action<OptionResult>. The beta-era named
    // ValidateSymbolResult<T> type was removed; a relapse to a beta-style
    // signature would not even compile, but this test pins the contract
    // so a future bump doesn't silently change it under us.
    [Fact]
    public void RunCommand_Validators_UseActionOptionResultDelegate()
    {
        var refreshOpt = new Option<bool>("--refresh");
        var javaOpt = new Option<string?>("--java-path");
        var skipJdkOpt = new Option<bool>("--skip-jdk-check");
        var run = RunCommand.Build(new NoopRunner(), new NoopJarSource(), new StubDetector(),
            refreshOpt, javaOpt, skipJdkOpt);

        var stateOpt = run.Options.Single(o => o.Name == "--state");
        Assert.NotEmpty(stateOpt.Validators);
        foreach (var v in stateOpt.Validators)
        {
            Assert.IsType<Action<OptionResult>>(v);
        }

        // Sanity: the named delegate from the beta API must not exist on
        // any validator. If a future bump reintroduces a named delegate
        // and we accidentally type-annotate against it, this catches it.
        var beta = Type.GetType("System.CommandLine.Parsing.ValidateSymbolResult`1, System.CommandLine");
        Assert.Null(beta);
    }

    [Fact]
    public void BuildArgumentList_BuildsExpectedFlags()
    {
        var args = new SyntheaArgs(
            State: "OH",
            City: "Cleveland",
            Gender: "M",
            AgeRange: "10-20",
            ModuleDir: new DirectoryInfo("/mods"),
            Modules: new[] { "flu" },
            Population: 5,
            Seed: 77,
            Config: new FileInfo("/cfg"),
            Zip: "44101",
            FhirVersion: "R4",
            InitialSnapshot: null,
            UpdatedSnapshot: null,
            DaysForward: null,
            Formats: new[] { "fhir" },
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: new[] { "--extra" });

        var list = RunCommand.BuildArgumentList(args);
        // Positional state/city/zip must come BEFORE passthru tokens so a
        // passthru value-flag can't consume the state code as its value. (A-9)
        var stateIdx = list.IndexOf("OH");
        var passthruIdx = list.IndexOf("--extra");
        Assert.True(stateIdx >= 0, "expected --state to appear in arg list");
        Assert.True(passthruIdx >= 0, "expected passthru token to appear in arg list");
        Assert.True(stateIdx < passthruIdx, $"state idx {stateIdx} should precede passthru idx {passthruIdx}");
        Assert.Contains("-p", list);
        Assert.Contains("5", list);
        Assert.Contains("-s", list);
        Assert.Contains("77", list);
        Assert.Contains("-c", list);
        Assert.Contains(Path.GetFullPath("/cfg"), list);
        Assert.Contains("--gender", list);
        Assert.Contains("M", list);
        Assert.Contains("--age-range", list);
        Assert.Contains("10-20", list);
        Assert.Contains("--module-dir", list);
        Assert.Contains(Path.GetFullPath("/mods"), list);
        Assert.Contains("--module", list);
        Assert.Contains("flu", list);
        Assert.Contains("--exporter.fhir.version=R4", list);
        Assert.Contains("--exporter.fhir.export=true", list);
        Assert.Contains("OH", list);
        Assert.Contains("Cleveland", list);
        Assert.Contains("44101", list);
        Assert.Contains("--extra", list);
    }

    [Fact]
    public void BuildArgumentList_FormatPlusAddFormat_EmitsBoth()
    {
        // Exclusive --format CSV disables all other formats. --add-format CCDA
        // then re-enables CCDA additively. Synthea reads the args in order,
        // so the later =true wins. (A-8)
        var args = new SyntheaArgs(
            State: null, City: null, Gender: null, AgeRange: null,
            ModuleDir: null, Modules: null, Population: null, Seed: null,
            Config: null, Zip: null, FhirVersion: null,
            InitialSnapshot: null, UpdatedSnapshot: null, DaysForward: null,
            Formats: new[] { "csv" },
            AdditionalFormats: new[] { "ccda" },
            Passthru: System.Array.Empty<string>());

        var list = RunCommand.BuildArgumentList(args);
        Assert.Contains("--exporter.csv.export=true", list);
        // Exclusive run sets ccda=false first; additive must come *after*.
        var ccdaFalseIdx = list.IndexOf("--exporter.ccda.export=false");
        var ccdaTrueIdx = list.IndexOf("--exporter.ccda.export=true");
        Assert.True(ccdaFalseIdx >= 0, "exclusive should emit ccda=false");
        Assert.True(ccdaTrueIdx >= 0, "additive should emit ccda=true");
        Assert.True(ccdaFalseIdx < ccdaTrueIdx,
            $"additive ccda=true ({ccdaTrueIdx}) must follow exclusive ccda=false ({ccdaFalseIdx}) so Synthea picks =true.");
    }

    [Fact]
    public void BuildArgumentList_AddFormatAlone_IsAdditiveOnly()
    {
        // Without --format, --add-format must only enable the named format —
        // not emit any =false entries for the others.
        var args = new SyntheaArgs(
            State: null, City: null, Gender: null, AgeRange: null,
            ModuleDir: null, Modules: null, Population: null, Seed: null,
            Config: null, Zip: null, FhirVersion: null,
            InitialSnapshot: null, UpdatedSnapshot: null, DaysForward: null,
            Formats: System.Array.Empty<string>(),
            AdditionalFormats: new[] { "csv" },
            Passthru: System.Array.Empty<string>());

        var list = RunCommand.BuildArgumentList(args);
        Assert.Contains("--exporter.csv.export=true", list);
        Assert.DoesNotContain(list, s => s.EndsWith("=false"));
    }

    [Fact]
    public void Passthru_DoesNotSwallowPositionalState()
    {
        // A passthru value-flag like `--some-flag OH` would, in the old
        // ordering, see "OH" emitted adjacent to the flag and consume it.
        // The fix is structural: emit state/city/zip first, passthru last.
        var args = new SyntheaArgs(
            State: "OH",
            City: null,
            Gender: null,
            AgeRange: null,
            ModuleDir: null,
            Modules: null,
            Population: null,
            Seed: null,
            Config: null,
            Zip: null,
            FhirVersion: null,
            InitialSnapshot: null,
            UpdatedSnapshot: null,
            DaysForward: null,
            Formats: System.Array.Empty<string>(),
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: new[] { "--some-flag" });

        var list = RunCommand.BuildArgumentList(args);
        var stateIdx = list.IndexOf("OH");
        var flagIdx = list.IndexOf("--some-flag");
        Assert.True(stateIdx >= 0);
        Assert.True(flagIdx >= 0);
        Assert.True(stateIdx < flagIdx,
            $"state position must precede passthru flag; got state={stateIdx} flag={flagIdx}");
        // And state must not appear adjacent-after the flag.
        Assert.NotEqual(flagIdx + 1, stateIdx);
    }

    [Fact]
    public void BuildArgumentList_Properties_EmittedAsDoubleDashFlags()
    {
        // (A6) Each --property KEY=VALUE becomes the documented Synthea
        // form `--KEY=VALUE`. Ordering: must come before positional state.
        var args = new SyntheaArgs(
            State: "OH", City: null, Gender: null, AgeRange: null,
            ModuleDir: null, Modules: null, Population: null, Seed: null,
            Config: null, Zip: null, FhirVersion: null,
            InitialSnapshot: null, UpdatedSnapshot: null, DaysForward: null,
            Formats: System.Array.Empty<string>(),
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: System.Array.Empty<string>(),
            Properties: new[] { "generate.timestep=86400000", "exporter.json.export=true" });

        var list = RunCommand.BuildArgumentList(args);
        Assert.Contains("--generate.timestep=86400000", list);
        Assert.Contains("--exporter.json.export=true", list);
        var stateIdx = list.IndexOf("OH");
        var propIdx = list.IndexOf("--generate.timestep=86400000");
        Assert.True(propIdx < stateIdx,
            $"properties (idx {propIdx}) should precede positional state (idx {stateIdx})");
    }

    [Theory]
    [InlineData("key=value", null)]                          // happy
    [InlineData("exporter.fhir.export=true", null)]          // dotted key
    [InlineData("my-key=value", null)]                       // hyphen ok
    [InlineData("my_key.sub=val", null)]                     // underscore + dot
    [InlineData("nokey", "must be KEY=VALUE")]
    [InlineData("=value", "must match")]
    [InlineData("1bad=value", "must match")]
    [InlineData("bad key=value", "must match")]
    [InlineData("key=a=b", "exactly one")]
    public void ValidateProperty_AppliesRules(string input, string? expectedFragment)
    {
        var err = RunCommand.ValidateProperty(input);
        if (expectedFragment is null)
        {
            Assert.Null(err);
        }
        else
        {
            Assert.NotNull(err);
            Assert.Contains(expectedFragment, err);
        }
    }

    [Fact]
    public void BuildArgumentList_UsCoreVersion_EmittedBeforeFhirExportLines()
    {
        // (A9) The us_core_version property must precede the format-export
        // block so Synthea sees the IG choice before evaluating exporters.
        var args = new SyntheaArgs(
            State: null, City: null, Gender: null, AgeRange: null,
            ModuleDir: null, Modules: null, Population: null, Seed: null,
            Config: null, Zip: null, FhirVersion: null,
            InitialSnapshot: null, UpdatedSnapshot: null, DaysForward: null,
            Formats: new[] { "fhir" },
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: System.Array.Empty<string>(),
            UsCoreVersion: "7");

        var list = RunCommand.BuildArgumentList(args);
        var enableIdx = list.IndexOf("--exporter.fhir.use_us_core_ig=true");
        var verIdx = list.IndexOf("--exporter.fhir.us_core_version=7");
        var exportTrueIdx = list.IndexOf("--exporter.fhir.export=true");
        Assert.True(enableIdx >= 0, "use_us_core_ig=true missing");
        Assert.True(verIdx >= 0, "us_core_version=7 missing");
        Assert.True(exportTrueIdx >= 0, "fhir.export=true missing");
        Assert.True(enableIdx < exportTrueIdx,
            $"use_us_core_ig (idx {enableIdx}) must precede fhir.export=true (idx {exportTrueIdx})");
        Assert.True(verIdx < exportTrueIdx,
            $"us_core_version (idx {verIdx}) must precede fhir.export=true (idx {exportTrueIdx})");
    }

    [Fact]
    public void BuildArgumentList_NoUsCoreVersion_OmitsBothProperties()
    {
        var args = new SyntheaArgs(
            State: null, City: null, Gender: null, AgeRange: null,
            ModuleDir: null, Modules: null, Population: null, Seed: null,
            Config: null, Zip: null, FhirVersion: null,
            InitialSnapshot: null, UpdatedSnapshot: null, DaysForward: null,
            Formats: System.Array.Empty<string>(),
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: System.Array.Empty<string>());

        var list = RunCommand.BuildArgumentList(args);
        Assert.DoesNotContain("--exporter.fhir.use_us_core_ig=true", list);
        Assert.DoesNotContain(list, s => s.StartsWith("--exporter.fhir.us_core_version="));
    }

    [Fact]
    public void CreateProcessStartInfo_UsesWorkingDirectoryAndJar()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "synthea-test-tmp");
        var hosting = new HostingOptions(
            Output: new DirectoryInfo(tmpDir),
            Refresh: false,
            JavaPath: "customjava");
        var args = new SyntheaArgs(
            State: null,
            City: null,
            Gender: null,
            AgeRange: null,
            ModuleDir: null,
            Modules: null,
            Population: null,
            Seed: null,
            Config: null,
            Zip: null,
            FhirVersion: null,
            InitialSnapshot: null,
            UpdatedSnapshot: null,
            DaysForward: null,
            Formats: System.Array.Empty<string>(),
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: System.Array.Empty<string>());

        var jar = new FileInfo(Path.Combine(tmpDir, "synthea.jar"));
        var psi = RunCommand.CreateProcessStartInfo(hosting, args, jar);
        Assert.Equal("customjava", psi.FileName);
        Assert.Equal(tmpDir, psi.WorkingDirectory);
        Assert.Contains("-jar", psi.ArgumentList);
        Assert.Contains(jar.FullName, psi.ArgumentList);
    }

    [Theory]
    [InlineData(new string[0], LogLevel.Information)]
    [InlineData(new[] { "--verbose" }, LogLevel.Debug)]
    [InlineData(new[] { "--quiet" }, LogLevel.Warning)]
    [InlineData(new[] { "--verbose", "--quiet" }, LogLevel.Debug)]   // verbose wins
    [InlineData(new[] { "run", "--quiet", "--output", "/tmp/x" }, LogLevel.Warning)]
    public void DetectLogLevel_MapsVerbosityFlags(string[] args, LogLevel expected)
    {
        Assert.Equal(expected, Program.DetectLogLevel(args));
    }

    private sealed class NoopRunner : IProcessRunner
    {
        public IProcess Start(ProcessStartInfo psi) => throw new NotSupportedException();
    }

    private sealed class StubDetector : IJavaDetector
    {
        public Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default)
            => Task.FromResult(new JavaProbeResult(true, 21, "21.0.5", null));
    }

    private sealed class NoopJarSource : IJarSource
    {
        public string CachePath => Path.GetTempPath();
        public FileInfo? TryFindCachedJar() => null;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => throw new NotSupportedException();
    }
}

