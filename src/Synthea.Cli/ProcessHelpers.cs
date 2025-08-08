using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace Synthea.Cli;

public interface IProcessRunner
{
    IProcess Start(ProcessStartInfo psi);
}

public interface IProcess : IDisposable
{
    StreamReader StandardOutput { get; }
    StreamReader StandardError { get; }
    Task WaitForExitAsync();
    int ExitCode { get; }
}

public sealed class DefaultProcessRunner : IProcessRunner
{
    public IProcess Start(ProcessStartInfo psi) => new ProcessWrapper(Process.Start(psi)!);

    private sealed class ProcessWrapper : IProcess
    {
        private readonly Process _proc;
        public ProcessWrapper(Process proc) => _proc = proc;
        public StreamReader StandardOutput => _proc.StandardOutput;
        public StreamReader StandardError => _proc.StandardError;
        public Task WaitForExitAsync() => _proc.WaitForExitAsync();
        public int ExitCode => _proc.ExitCode;
        public void Dispose() => _proc.Dispose();
    }
}

internal static class ProcessHelpers
{
    internal static async Task Relay(StreamReader src, TextWriter dest)
    {
        string? line;
        while ((line = await src.ReadLineAsync()) is not null)
            dest.WriteLine(line);
    }
}
