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

    [SkippableFact]
    public async Task Synthea_CLI_Wrapper_Generates_Output()
    {
        var dllPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Synthea.Cli", "bin", "Release", "net8.0", "Synthea.Cli.dll"));
        bool dllExists = File.Exists(dllPath);
        bool globalExists = CommandExists("synthea");
        if (!dllExists && !globalExists)
        {
            Console.WriteLine("Skipping test: Synthea CLI wrapper not found");
            return;
        }
        if (!CommandExists("java"))
        {
            Console.WriteLine("Skipping test: Java not found");
            return;
        }

        string command = dllExists
            ? $"dotnet \"{dllPath}\" run --output ./output --population 1"
            : "synthea run --output ./output --population 1";

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c {command}")
            : new ProcessStartInfo("bash", $"-c \"{command}\"");
        psi.WorkingDirectory = _workDir;
        psi.RedirectStandardError = true;
        psi.RedirectStandardOutput = true;

        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();

        Assert.Equal(0, proc.ExitCode);

        var outDir = Path.Combine(_workDir, "output");
        Assert.True(Directory.Exists(outDir));
        Assert.NotEmpty(Directory.GetFiles(outDir, "*", SearchOption.AllDirectories));
    }
}
