using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace Synthea.Cli;

internal interface IProcessRunner
{
    IProcess Start(ProcessStartInfo psi);
}

internal interface IProcess : IDisposable
{
    StreamReader StandardOutput { get; }
    StreamReader StandardError { get; }
    Task WaitForExitAsync();
    int ExitCode { get; }

    // Default no-op so existing test fakes (CapturingRunner.StubProcess,
    // FakeRunner.FakeProcess) keep working without modification.
    void Kill(bool entireProcessTree) { }
}

internal sealed class DefaultProcessRunner : IProcessRunner
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
        public void Kill(bool entireProcessTree) => _proc.Kill(entireProcessTree);
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

    // Relay each line AND retain the last `capacity` lines in a ring buffer
    // so the caller can inspect tail-of-stderr for a remediation match after
    // the process exits non-zero. Buffer is bounded to avoid pinning a
    // long-running Synthea run's full stderr in memory. (C6)
    //
    // D2: the optional `onLine` callback fires synchronously for each
    // line, before the line hits `dest`. Callers use it to feed the
    // progress parser; keeping it synchronous keeps ordering predictable
    // without forcing a queue or async fence.
    internal static async Task<IReadOnlyList<string>> RelayAndCapture(
        StreamReader src, TextWriter dest, int capacity = 50, Action<string>? onLine = null)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        var ring = new List<string>(capacity);
        string? line;
        while ((line = await src.ReadLineAsync()) is not null)
        {
            onLine?.Invoke(line);
            dest.WriteLine(line);
            if (ring.Count >= capacity) ring.RemoveAt(0);
            ring.Add(line);
        }
        return ring;
    }
}
