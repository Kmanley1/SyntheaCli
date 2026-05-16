using System;
using System.IO;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
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
        var run = RunCommand.Build(refreshOpt, javaOpt);

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
            Passthru: System.Array.Empty<string>());

        var jar = new FileInfo(Path.Combine(tmpDir, "synthea.jar"));
        var psi = RunCommand.CreateProcessStartInfo(hosting, args, jar);
        Assert.Equal("customjava", psi.FileName);
        Assert.Equal(tmpDir, psi.WorkingDirectory);
        Assert.Contains("-jar", psi.ArgumentList);
        Assert.Contains(jar.FullName, psi.ArgumentList);
    }
}

