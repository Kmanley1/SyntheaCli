using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Synthea.Cli;

// `synthea doctor` — runs six environment checks and prints a one-line
// status per check. Exit policy: any FAIL → exit 1; OK + WARN-only → exit 0
// (warnings are informational; `--jar` lets users work around most of them).
// (B6)
internal static class DoctorCommand
{
    // Thresholds promoted to constants so tests pin behavior without
    // magic numbers drifting between source and asserts.
    internal const int CachedJarWarnAgeDays = 90;
    internal const int CachedJarStaleAgeDays = 180;
    internal const long DiskSpaceFailBytes = 200L * 1024 * 1024;        // 200 MB
    internal const long DiskSpaceWarnBytes = 1L * 1024 * 1024 * 1024;   // 1 GB
    internal static readonly TimeSpan GitHubProbeTimeout = TimeSpan.FromSeconds(5);

    internal static Command Build(
        IJarSource jarSource,
        IJavaDetector javaDetector,
        IFileSystem fileSystem,
        IGitHubReachabilityProbe gitHubProbe,
        IDiskSpaceProbe diskProbe,
        Option<string?> javaOpt)
    {
        var cmd = new Command("doctor", "Run environment checks and report Java, cache, config, network, and disk status");

        cmd.SetAction(async (ParseResult parseResult, CancellationToken cancelToken) =>
        {
            var javaPathArg = parseResult.GetValue(javaOpt);
            var javaPath = string.IsNullOrWhiteSpace(javaPathArg) ? "java" : javaPathArg!;

            var checks = BuildChecks(
                javaPath: javaPath,
                javaDetector: javaDetector,
                jarSource: jarSource,
                fileSystem: fileSystem,
                gitHubProbe: gitHubProbe,
                diskProbe: diskProbe,
                configPath: null,
                clock: null);

            var results = new List<DoctorCheckResult>(checks.Count);
            foreach (var check in checks)
                results.Add(await check.RunAsync(cancelToken));

            Console.WriteLine(FormatReport(results));
            return ExitCodeFor(results);
        });

        return cmd;
    }

    internal static IReadOnlyList<IDoctorCheck> BuildChecks(
        string javaPath,
        IJavaDetector javaDetector,
        IJarSource jarSource,
        IFileSystem fileSystem,
        IGitHubReachabilityProbe gitHubProbe,
        IDiskSpaceProbe diskProbe,
        string? configPath,
        Func<DateTime>? clock)
    {
        var now = clock ?? (() => DateTime.UtcNow);
        return new IDoctorCheck[]
        {
            new JavaCheck(javaPath, javaDetector),
            new CacheDirCheck(jarSource, fileSystem),
            new CachedJarCheck(jarSource, now),
            new ConfigCheck(configPath),
            new GitHubCheck(gitHubProbe),
            new DiskSpaceCheck(jarSource, diskProbe),
        };
    }

    internal static int ExitCodeFor(IReadOnlyList<DoctorCheckResult> results)
        => results.Any(r => r.Severity == DoctorSeverity.Fail) ? 1 : 0;

    internal static string FormatReport(IReadOnlyList<DoctorCheckResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("synthea doctor — environment check");
        sb.AppendLine();

        // Pad the check name to the widest one so the message column lines
        // up. Constant width across runs would be tidier but pads the
        // common case unnecessarily.
        var nameWidth = results.Count == 0 ? 0 : results.Max(r => r.Name.Length);
        foreach (var r in results)
        {
            sb.Append(SeverityTag(r.Severity));
            sb.Append("   ");
            sb.Append(r.Name.PadRight(nameWidth));
            sb.Append("   ");
            sb.AppendLine(r.Message);
        }

        sb.AppendLine();
        var failCount = results.Count(r => r.Severity == DoctorSeverity.Fail);
        var warnCount = results.Count(r => r.Severity == DoctorSeverity.Warn);
        if (failCount > 0)
            sb.Append($"{failCount} critical issue{(failCount == 1 ? "" : "s")} found.");
        else if (warnCount > 0)
            sb.Append($"{warnCount} warning{(warnCount == 1 ? "" : "s")}; CLI should still run.");
        else
            sb.Append("All checks passed.");
        return sb.ToString();
    }

    private static string SeverityTag(DoctorSeverity severity) => severity switch
    {
        DoctorSeverity.Ok => "[ OK ]",
        DoctorSeverity.Warn => "[WARN]",
        DoctorSeverity.Fail => "[FAIL]",
        _ => "[????]",
    };

    // ----- Check implementations ----------------------------------------

    internal sealed class JavaCheck : IDoctorCheck
    {
        private readonly string _javaPath;
        private readonly IJavaDetector _detector;
        public JavaCheck(string javaPath, IJavaDetector detector)
        {
            _javaPath = javaPath;
            _detector = detector;
        }

        public async Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken)
        {
            var probe = await _detector.ProbeAsync(_javaPath, cancelToken);
            if (!probe.Found)
                return new("Java", DoctorSeverity.Fail,
                    $"not found at '{_javaPath}' ({probe.ErrorMessage ?? "no java -version output"})");
            if (probe.MajorVersion is null)
                return new("Java", DoctorSeverity.Fail,
                    $"could not parse version from '{_javaPath}' (raw: {probe.RawVersionString ?? "?"})");
            if (probe.MajorVersion.Value < 17)
                return new("Java", DoctorSeverity.Fail,
                    $"Java {probe.MajorVersion} (Synthea requires 17 or later — see https://adoptium.net)");
            return new("Java", DoctorSeverity.Ok, $"Java {probe.RawVersionString ?? probe.MajorVersion.ToString()}");
        }
    }

    internal sealed class CacheDirCheck : IDoctorCheck
    {
        private readonly IJarSource _jarSource;
        private readonly IFileSystem _fs;
        public CacheDirCheck(IJarSource jarSource, IFileSystem fs)
        {
            _jarSource = jarSource;
            _fs = fs;
        }

        public Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken)
        {
            var dir = _jarSource.CachePath;
            if (_fs.TryProbeWrite(dir, out var err))
                return Task.FromResult(new DoctorCheckResult("Cache dir", DoctorSeverity.Ok, $"{dir} (writeable)"));
            return Task.FromResult(new DoctorCheckResult("Cache dir", DoctorSeverity.Fail, $"{dir} not writeable: {err}"));
        }
    }

    internal sealed class CachedJarCheck : IDoctorCheck
    {
        private readonly IJarSource _jarSource;
        private readonly Func<DateTime> _now;
        public CachedJarCheck(IJarSource jarSource, Func<DateTime> now)
        {
            _jarSource = jarSource;
            _now = now;
        }

        public Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken)
        {
            var jar = _jarSource.TryFindCachedJar();
            if (jar is null)
                return Task.FromResult(new DoctorCheckResult(
                    "Cached JAR", DoctorSeverity.Warn,
                    "no JAR cached yet — will download on first run"));
            var ageDays = (_now() - jar.LastWriteTimeUtc).TotalDays;
            var sizeMb = jar.Length / (1024.0 * 1024.0);
            var detail = $"{jar.Name} ({(int)ageDays}d old, {sizeMb:0} MB)";
            if (ageDays >= CachedJarStaleAgeDays)
                return Task.FromResult(new DoctorCheckResult(
                    "Cached JAR", DoctorSeverity.Warn, detail + " — consider --refresh"));
            if (ageDays >= CachedJarWarnAgeDays)
                return Task.FromResult(new DoctorCheckResult(
                    "Cached JAR", DoctorSeverity.Warn, detail));
            return Task.FromResult(new DoctorCheckResult(
                "Cached JAR", DoctorSeverity.Ok, detail));
        }
    }

    internal sealed class ConfigCheck : IDoctorCheck
    {
        private readonly string? _configPath;
        public ConfigCheck(string? configPath) => _configPath = configPath;

        public Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken)
        {
            var path = _configPath ?? CliConfig.DefaultPath();
            if (!File.Exists(path))
                return Task.FromResult(new DoctorCheckResult(
                    "Config", DoctorSeverity.Ok, $"no config file at {path} (defaults apply)"));
            try
            {
                _ = CliConfig.LoadOrThrow(path);
                return Task.FromResult(new DoctorCheckResult(
                    "Config", DoctorSeverity.Ok, $"{path} (valid)"));
            }
            catch (CliConfigException ex)
            {
                return Task.FromResult(new DoctorCheckResult(
                    "Config", DoctorSeverity.Fail, ex.Message));
            }
        }
    }

    internal sealed class GitHubCheck : IDoctorCheck
    {
        private readonly IGitHubReachabilityProbe _probe;
        public GitHubCheck(IGitHubReachabilityProbe probe) => _probe = probe;

        public async Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken)
        {
            var r = await _probe.PingAsync(GitHubProbeTimeout, cancelToken);
            if (r.Reachable)
            {
                var elapsed = r.ElapsedMilliseconds.HasValue ? $", {r.ElapsedMilliseconds}ms" : "";
                return new("GitHub", DoctorSeverity.Ok, $"api.github.com reachable (HTTP {r.StatusCode}{elapsed})");
            }
            if (r.StatusCode is int code)
                return new("GitHub", DoctorSeverity.Warn,
                    $"api.github.com returned HTTP {code} — --jar can bypass GitHub entirely");
            return new("GitHub", DoctorSeverity.Warn,
                $"api.github.com unreachable ({r.ErrorMessage ?? "unknown error"}) — --jar can bypass GitHub entirely");
        }
    }

    internal sealed class DiskSpaceCheck : IDoctorCheck
    {
        private readonly IJarSource _jarSource;
        private readonly IDiskSpaceProbe _probe;
        public DiskSpaceCheck(IJarSource jarSource, IDiskSpaceProbe probe)
        {
            _jarSource = jarSource;
            _probe = probe;
        }

        public Task<DoctorCheckResult> RunAsync(CancellationToken cancelToken)
        {
            var dir = _jarSource.CachePath;
            var free = _probe.GetFreeBytes(dir);
            if (free is null)
                return Task.FromResult(new DoctorCheckResult(
                    "Disk space", DoctorSeverity.Warn,
                    $"could not determine free space for {dir}"));
            var bytes = free.Value;
            var human = FormatBytes(bytes);
            if (bytes < DiskSpaceFailBytes)
                return Task.FromResult(new DoctorCheckResult(
                    "Disk space", DoctorSeverity.Fail,
                    $"only {human} free in cache dir — Synthea JAR is ~180 MB"));
            if (bytes < DiskSpaceWarnBytes)
                return Task.FromResult(new DoctorCheckResult(
                    "Disk space", DoctorSeverity.Warn,
                    $"{human} free in cache dir"));
            return Task.FromResult(new DoctorCheckResult(
                "Disk space", DoctorSeverity.Ok, $"{human} free in cache dir"));
        }

        private static string FormatBytes(long bytes)
        {
            const double GB = 1024.0 * 1024.0 * 1024.0;
            const double MB = 1024.0 * 1024.0;
            if (bytes >= GB) return $"{bytes / GB:0.0} GB";
            return $"{bytes / MB:0} MB";
        }
    }
}
