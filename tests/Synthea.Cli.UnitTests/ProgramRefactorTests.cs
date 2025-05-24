using System.IO;
using System.Collections.Generic;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class ProgramRefactorTests
{
    [Fact]
    public void BuildArgumentList_BuildsExpectedFlags()
    {
        var opts = new Program.RunOptions(
            Output: new DirectoryInfo("/tmp"),
            Refresh: false,
            JavaPath: "java",
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

        var list = Program.BuildArgumentList(opts);
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
    public void CreateProcessStartInfo_UsesWorkingDirectoryAndJar()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "synthea-test-tmp");
        var opts = new Program.RunOptions(
            Output: new DirectoryInfo(tmpDir),
            Refresh: false,
            JavaPath: "customjava",
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
        var psi = Program.CreateProcessStartInfo(opts, jar);
        Assert.Equal("customjava", psi.FileName);
        Assert.Equal(tmpDir, psi.WorkingDirectory);
        Assert.Contains("-jar", psi.ArgumentList);
        Assert.Contains(jar.FullName, psi.ArgumentList);
    }
}

