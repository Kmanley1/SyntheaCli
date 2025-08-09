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
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var exe = OperatingSystem.IsWindows() && Path.GetExtension(cmd) != ".exe" ? cmd + ".exe" : cmd;
        return paths.Any(p => File.Exists(Path.Combine(p, exe)));
    }

    private async Task<(int exitCode, string stdOut, string stdErr)> RunCliCommandAsync(string args, string? workingDir = null)
    {
        // Try multiple possible paths for the DLL (Release first, then Debug for development)
        var possibleDllPaths = new[]
        {
            // Primary Release paths
            Path.GetFullPath(Path.Combine("..", "..", "..", "..", "artifacts", "bin", "Release", "net8.0", "Synthea.Cli.dll")),
            Path.GetFullPath(Path.Combine("..", "..", "..", "..", "src", "Synthea.Cli", "bin", "Release", "net8.0", "Synthea.Cli.dll")),
            Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Synthea.Cli", "bin", "Release", "net8.0", "Synthea.Cli.dll")),
            // Fallback Debug paths for development
            Path.GetFullPath(Path.Combine("..", "..", "..", "..", "artifacts", "bin", "Debug", "net8.0", "Synthea.Cli.dll")),
            Path.GetFullPath(Path.Combine("..", "..", "..", "..", "src", "Synthea.Cli", "bin", "Debug", "net8.0", "Synthea.Cli.dll"))
        };
        
        string? dllPath = possibleDllPaths.FirstOrDefault(File.Exists);
        bool dllExists = dllPath != null;
        bool globalExists = CommandExists("synthea");
        
        if (!dllExists && !globalExists)
        {
            var searchedPaths = string.Join("\n  ", possibleDllPaths);
            throw new SkipTestException($"Synthea CLI wrapper not found. Skipping integration test.\nSearched paths:\n  {searchedPaths}\n\nTo fix this, run: dotnet build -c Release");
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
            
        // Debug: Log file details if count doesn't match expected
        if (patientFiles.Length != population)
        {
            var allFileNames = string.Join(", ", allFiles.Select(Path.GetFileName));
            var patientFileNames = string.Join(", ", patientFiles.Select(Path.GetFileName));
            throw new InvalidOperationException(
                $"Expected {population} patient files, found {patientFiles.Length}. " +
                $"All files: [{allFileNames}]. Patient files: [{patientFileNames}]");
        }
        
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
            Assert.Equal(expectedCount, files.Length);
        }
        catch (SkipTestException ex)
        {
            // Mark as skipped by failing with a clear message (xUnit does not support runtime skip natively)
            Assert.Fail($"SKIPPED: {ex.Message}");
        }
    }
}
