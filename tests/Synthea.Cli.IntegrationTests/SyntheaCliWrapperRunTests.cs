using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Synthea.Cli.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkippableFactAttribute : FactAttribute { }

[Trait("Category", "Integration")]
public class SyntheaCliWrapperRunTests : IDisposable
{
    private readonly string _workDir;

    public SyntheaCliWrapperRunTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_workDir, true); } catch { }
    }

    private static bool CommandExists(string cmd)
    {
        try
        {
            // Try running the command to see if it exists
            var psi = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("cmd.exe", $"/c where {cmd}")
                : new ProcessStartInfo("which", cmd);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
            return proc?.ExitCode == 0;
        }
        catch
        {
            // Fallback to PATH search
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var exe = OperatingSystem.IsWindows() && Path.GetExtension(cmd) != ".exe" ? cmd + ".exe" : cmd;
            return paths.Any(p => File.Exists(Path.Combine(p, exe)));
        }
    }

    private static string? FindRepoRoot()
    {
        // Walk up from the test assembly's location until we find the solution
        // file. Robust to changes in BaseOutputPath / working directory.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Synthea.Cli.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static (string? dllPath, string[] searched) FindCliDll()
    {
        var root = FindRepoRoot();
        if (root is null) return (null, Array.Empty<string>());
        // Prefer Release (CI publishes Release) but accept Debug for local dev.
        var candidates = new[]
        {
            Path.Combine(root, "src", "Synthea.Cli", "bin", "Release", "net8.0", "Synthea.Cli.dll"),
            Path.Combine(root, "src", "Synthea.Cli", "bin", "Debug", "net8.0", "Synthea.Cli.dll"),
        };
        return (candidates.FirstOrDefault(File.Exists), candidates);
    }

    private async Task<(int exitCode, string stdOut, string stdErr)> RunCliCommandAsync(string args, string? workingDir = null)
    {
        var (dllPath, searched) = FindCliDll();
        bool dllExists = dllPath is not null;
        bool globalExists = CommandExists("synthea");

        if (!dllExists && !globalExists)
        {
            var searchedPaths = string.Join("\n  ", searched);
            throw new SkipTestException($"Synthea CLI wrapper not found. Skipping integration test.\nSearched paths:\n  {searchedPaths}\n\nTo fix this, run: dotnet build");
        }
        if (!CommandExists("java"))
        {
            throw new SkipTestException("Java not found. Skipping integration test.\nTo fix this:\n" +
                "  - Install Java 11 or newer: https://adoptium.net/\n" +
                "  - Or run: .\\setup-test-environment.ps1 -InstallJava");
        }

        string command = dllExists
            ? $"dotnet \"{dllPath}\" {args}"
            : $"synthea {args}";

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c {command}")
            : new ProcessStartInfo("bash", $"-c \"{command}\"");
        psi.WorkingDirectory = workingDir ?? _workDir;
        psi.RedirectStandardError = true;
        psi.RedirectStandardOutput = true;

        using var proc = Process.Start(psi)!;
        var stdOutTask = proc.StandardOutput.ReadToEndAsync();
        var stdErrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;
        return (proc.ExitCode, stdOut, stdErr);
    }

    private const string FhirSubDir = "output";
    private const string FhirTypeDir = "fhir";
    private const string HospitalInfo = "hospitalInformation";
    private const string PractitionerInfo = "practitionerInformation";

    private async Task<string[]> RunSyntheaAndGetPatientFiles(int population)
    {
        // Use a unique output directory per test run to avoid interference
        var testOutputDir = $"output_{Guid.NewGuid():N}";
        var args = $"run --output ./{testOutputDir} --population {population}";
        var (exitCode, stdOut, stdErr) = await RunCliCommandAsync(args);

        Assert.Equal(0, exitCode);

        var fhirDir = Path.Combine(_workDir, testOutputDir, FhirSubDir, FhirTypeDir);
        EnsureFhirDirExists(fhirDir, stdOut, stdErr);
        
        // Get all JSON files and filter out known non-patient files
        var allFiles = Directory.GetFiles(fhirDir, "*.json", SearchOption.TopDirectoryOnly);
        var patientFiles = allFiles
            .Where(f => !Path.GetFileName(f).Contains(HospitalInfo) && 
                       !Path.GetFileName(f).Contains(PractitionerInfo))
            .ToArray();
            
        // Synthea's -p N means "N living patients at simulation end"; the FHIR
        // exporter also writes deceased patients generated along the way, so
        // the actual file count is >= N (not == N). Don't fail on that here.
        return patientFiles;
    }

    private void EnsureFhirDirExists(string fhirDir, string stdOut, string stdErr)
    {
        if (!Directory.Exists(fhirDir))
        {
            var structure = string.Join("\n", Directory.GetFileSystemEntries(_workDir, "*", SearchOption.AllDirectories));
            var message = $"FHIR directory not found: {fhirDir}\n\nDirectory structure under work dir:\n{structure}\n\nSTDOUT:\n{stdOut}\n\nSTDERR:\n{stdErr}";
            throw new DirectoryNotFoundException(message);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public async Task Synthea_CLI_Wrapper_Generates_Correct_Number_Of_Patient_Files(int population, int expectedCount)
    {
        try
        {
            var files = await RunSyntheaAndGetPatientFiles(population);
            // See RunSyntheaAndGetPatientFiles: file count is >= population.
            Assert.True(files.Length >= expectedCount,
                $"Expected at least {expectedCount} patient file(s), found {files.Length}.");
        }
        catch (SkipTestException ex)
        {
            // Mark as skipped by failing with a clear message (xUnit does not support runtime skip natively)
            Assert.Fail($"SKIPPED: {ex.Message}");
        }
    }
}
