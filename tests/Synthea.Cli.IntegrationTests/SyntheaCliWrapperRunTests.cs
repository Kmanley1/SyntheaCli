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
        var dllPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Synthea.Cli", "bin", "Release", "net8.0", "Synthea.Cli.dll"));
        bool dllExists = File.Exists(dllPath);
        bool globalExists = CommandExists("synthea");
        if (!dllExists && !globalExists)
        {
            throw new SkipTestException("Synthea CLI wrapper not found. Skipping integration test.");
        }
        if (!CommandExists("java"))
        {
            throw new SkipTestException("Java not found. Skipping integration test.");
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

    private const string OutputDir = "output";
    private const string FhirSubDir = "output";
    private const string FhirTypeDir = "fhir";
    private const string HospitalInfo = "hospitalInformation";
    private const string PractitionerInfo = "practitionerInformation";

    private async Task<string[]> RunSyntheaAndGetPatientFiles(int population)
    {
        var testOutputDir = OutputDir;
        var args = $"run --output ./{testOutputDir} --population {population}";
        var (exitCode, stdOut, stdErr) = await RunCliCommandAsync(args);

        Assert.Equal(0, exitCode);

        var fhirDir = Path.Combine(_workDir, testOutputDir, FhirSubDir, FhirTypeDir);
        EnsureFhirDirExists(fhirDir, stdOut, stdErr);
        return Directory.GetFiles(fhirDir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !f.Contains(HospitalInfo) && !f.Contains(PractitionerInfo))
            .ToArray();
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
