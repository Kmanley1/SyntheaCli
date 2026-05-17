using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Synthea.Cli;

// Three-level severity matches the plan's exit-code policy: Ok and Warn
// are both exit 0; Fail is the only thing that promotes the run to exit 1.
internal enum DoctorSeverity
{
    Ok,
    Warn,
    Fail,
}

internal sealed record DoctorCheckResult(string Name, DoctorSeverity Severity, string Message);

internal interface IDoctorCheck
{
    Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken);
}

// Tiny shim around File.WriteAllText/File.Delete so the cache-dir-writeable
// check can be tested without touching the real filesystem. Production
// uses DefaultFileSystem; tests substitute an in-memory stub.
internal interface IFileSystem
{
    bool TryProbeWrite(string directoryPath, out string? errorMessage);
}

internal sealed class DefaultFileSystem : IFileSystem
{
    public bool TryProbeWrite(string directoryPath, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);
            var probe = Path.Combine(directoryPath, ".synthea-cli-write-probe-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (IOException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
