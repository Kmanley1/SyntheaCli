using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Synthea.Cli;

internal sealed record JavaProbeResult(
    bool Found,
    int? MajorVersion,
    string? RawVersionString,
    string? ErrorMessage);

internal interface IJavaDetector
{
    Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default);
}

internal sealed class JavaDetector : IJavaDetector
{
    private readonly IProcessRunner _runner;

    public JavaDetector(IProcessRunner runner) => _runner = runner;

    public async Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(javaPath))
            return new JavaProbeResult(false, null, null, "Java path was empty.");

        var psi = new ProcessStartInfo(javaPath, "-version")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var proc = _runner.Start(psi);
            // `java -version` prints to stderr, but redirect both to be safe.
            var stderr = proc.StandardError.ReadToEndAsync(cancelToken);
            var stdout = proc.StandardOutput.ReadToEndAsync(cancelToken);
            await Task.WhenAll(stderr, stdout, proc.WaitForExitAsync());
            return Parse(stderr.Result + stdout.Result);
        }
        catch (Exception ex)
        {
            return new JavaProbeResult(false, null, null, ex.Message);
        }
    }

    // Old style: "java version "1.8.0_201"" — major version is the integer after the leading "1.".
    // New style (Java 9+): "openjdk version "21.0.5"" — major version is the first integer.
    internal static JavaProbeResult Parse(string output)
    {
        if (string.IsNullOrEmpty(output))
            return new JavaProbeResult(false, null, null, "Empty output from java -version.");

        var match = Regex.Match(output, @"version\s+""(?<v>[\d._]+)""", RegexOptions.IgnoreCase);
        if (!match.Success)
            return new JavaProbeResult(false, null, null,
                "Could not parse Java version from output: " + output.Trim());

        var raw = match.Groups["v"].Value;
        var parts = raw.Split(new[] { '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], out var first))
            return new JavaProbeResult(false, null, raw, "Could not parse major version from: " + raw);

        var major = first == 1 && parts.Length > 1 && int.TryParse(parts[1], out var second)
            ? second
            : first;
        return new JavaProbeResult(true, major, raw, null);
    }
}
