using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

// F3: Golden-file --help tests. The CLI's surface area is part of its
// contract; option renames, hidden defaults moving into descriptions,
// or accidentally deleted commands all show up here as a diff before
// they reach users. Goldens live next to this file in `golden/`.
//
// We spawn the actual `Synthea.Cli.dll` via `dotnet exec` for each
// case rather than calling Program.RunAsync in-process. Reason:
// Console.SetOut is a process-global; with test parallelism on (A-12)
// any concurrent test that writes to Console.Out would corrupt the
// captured help. Subprocess isolation gives a reliable capture with
// no Program.cs surface change.
//
// To intentionally update a golden after a real surface change:
//   $env:SYNTHEA_CLI_REGENERATE_HELP_GOLDENS = "1"
//   dotnet test tests/Synthea.Cli.UnitTests --filter HelpSurfaceTests
// The tests re-write the goldens in the *source* tree (not the test
// output dir), commit them with the feature, and the suite goes green
// on the next run.
public class HelpSurfaceTests
{
    private const string RegenerateEnvVar = "SYNTHEA_CLI_REGENERATE_HELP_GOLDENS";

    [Theory]
    [InlineData("help-root.txt", new[] { "--help" })]
    [InlineData("help-run.txt", new[] { "run", "--help" })]
    [InlineData("help-cache.txt", new[] { "cache", "--help" })]
    [InlineData("help-doctor.txt", new[] { "doctor", "--help" })]
    [InlineData("help-modules.txt", new[] { "modules", "--help" })]
    public async Task Help_MatchesGoldenFile(string goldenName, string[] args)
    {
        var actual = await CaptureHelpAsync(args);
        var goldenPath = ResolveGoldenSourcePath(goldenName);

        if (string.Equals(Environment.GetEnvironmentVariable(RegenerateEnvVar), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllText(goldenPath, actual);
            return;
        }

        Assert.True(File.Exists(goldenPath),
            $"Golden file '{goldenPath}' not found. Generate with: " +
            $"{RegenerateEnvVar}=1 dotnet test --filter HelpSurfaceTests");

        var expected = Normalize(File.ReadAllText(goldenPath));
        var normalizedActual = Normalize(actual);
        if (!string.Equals(expected, normalizedActual, StringComparison.Ordinal))
        {
            // Surfacing both texts in the failure message would dwarf
            // the rest of the test output; point the developer to the
            // regeneration command instead and show the first diverging
            // line so a glance tells them what changed.
            Assert.Fail(
                $"Help surface drift for '{goldenName}'. " +
                $"If the change is intentional, regenerate with: " +
                $"{RegenerateEnvVar}=1 dotnet test --filter HelpSurfaceTests" +
                Environment.NewLine +
                FirstDifference(expected, normalizedActual));
        }
    }

    private static async Task<string> CaptureHelpAsync(string[] args)
    {
        var dllPath = Path.Combine(AppContext.BaseDirectory, "Synthea.Cli.dll");
        Assert.True(File.Exists(dllPath),
            $"Synthea.Cli.dll not found at '{dllPath}'. Run `dotnet build` first.");

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            // CLI emits UTF-8 (Program.Main sets Console.OutputEncoding).
            // On Windows the default pipe-read encoding is the ANSI code
            // page, which mojibakes non-ASCII (e.g. "…" → "ΓÇª"). Force
            // UTF-8 so what we read matches what the binary wrote.
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            // Pin a wide terminal so System.CommandLine's help renderer
            // doesn't soft-wrap based on whatever happens to be the CI
            // runner's COLUMNS at the moment.
            EnvironmentVariables = { ["COLUMNS"] = "200" }
        };
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(dllPath);
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        // --help conventionally exits 0; if a non-zero shows up here we
        // want the assertion failure to surface the diagnostic.
        Assert.Equal(0, proc.ExitCode);
        return stdout;
    }

    private static string Normalize(string text)
    {
        // Goldens are checked in with LF endings but Git on Windows
        // converts them to CRLF on checkout. Normalize both sides to
        // LF so the comparison is EOL-independent.
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string FirstDifference(string expected, string actual)
    {
        var expLines = expected.Split('\n');
        var actLines = actual.Split('\n');
        var max = Math.Max(expLines.Length, actLines.Length);
        for (var i = 0; i < max; i++)
        {
            var e = i < expLines.Length ? expLines[i] : "<missing>";
            var a = i < actLines.Length ? actLines[i] : "<missing>";
            if (!string.Equals(e, a, StringComparison.Ordinal))
            {
                return $"First diff at line {i + 1}:" + Environment.NewLine +
                       $"  expected: {e}" + Environment.NewLine +
                       $"  actual:   {a}";
            }
        }
        return "Outputs differ only in trailing content.";
    }

    private static string ResolveGoldenSourcePath(string fileName)
    {
        // Walk up from the test binary location to the repo root, then
        // descend to the source-tree copy of the golden. We deliberately
        // target the *source* path so regeneration produces a file the
        // developer can commit; the build-output copy is rebuilt next
        // time `dotnet build` runs.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Synthea.Cli.sln")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "tests", "Synthea.Cli.UnitTests", "golden", fileName);
    }
}
