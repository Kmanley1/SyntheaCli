using System;
using System.CommandLine;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Synthea.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Synthea emits patient names with diacritics; force UTF-8 so they don't
        // mojibake on Windows code pages (cp437/cp1252).
        Console.OutputEncoding = Encoding.UTF8;

        // Pre-parse the verbosity switches so the logger provider is built at
        // the right minimum level before System.CommandLine ever sees the args.
        // The flags are also declared as Recursive options below so --help and
        // the parser still know about them.
        var level = DetectLogLevel(args);
        await using var services = BuildDefaultServices(level);
        return await RunAsync(args, services);
    }

    internal static ServiceProvider BuildDefaultServices(LogLevel minimumLevel = LogLevel.Information)
    {
        var sc = new ServiceCollection();
        sc.AddLogging(b =>
        {
            b.SetMinimumLevel(minimumLevel);
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            // Route logs to stderr so stdout stays a clean data channel
            // (e.g. `run --dry-run | sh`, or piping Synthea's own output).
            b.Services.Configure<Microsoft.Extensions.Logging.Console.ConsoleLoggerOptions>(
                o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        });
        sc.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        sc.AddSingleton<IJavaDetector, JavaDetector>();
        sc.AddSingleton<IJarSource>(sp => new JarManager(
            http: BuildHttpClient(CliConfig.Load()),
            logger: sp.GetRequiredService<ILogger<JarManager>>()));
        // (B6) `synthea doctor` needs lightweight probes. The HttpClient
        // used for the reachability probe is independent of the JarManager
        // client so the proxy/UA chain stays single-purpose there.
        sc.AddSingleton<IFileSystem, DefaultFileSystem>();
        sc.AddSingleton<IDiskSpaceProbe, DefaultDiskSpaceProbe>();
        sc.AddSingleton<IGitHubReachabilityProbe>(_ => new HttpGitHubReachabilityProbe(BuildHttpClient(CliConfig.Load())));
        return sc.BuildServiceProvider();
    }

    internal static HttpClient BuildHttpClient(CliConfig config)
    {
        // Proxy is wired at construction time: HttpClient does not expose its
        // handler for runtime mutation, so per-call proxy overrides aren't
        // possible without rebuilding the client. CLI/env/config still apply
        // in the usual precedence order. (A-5)
        var proxyUrl = CliConfig.Resolve(null, "HTTPS_PROXY", config.HttpsProxy)
                    ?? CliConfig.Resolve(null, "HTTP_PROXY", null);
        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(proxyUrl))
        {
            handler.Proxy = new WebProxy(proxyUrl);
            handler.UseProxy = true;
        }
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("synthea-cli", GetCliVersion()));
        return client;
    }

    internal static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var runner = services.GetRequiredService<IProcessRunner>();
        var jarSource = services.GetRequiredService<IJarSource>();
        var javaDetector = services.GetRequiredService<IJavaDetector>();
        // Doctor-only services: fall back to defaults so test fixtures that
        // pre-date Phase 9 don't have to register every interface to exercise
        // `synthea run`. Production wires real impls via BuildDefaultServices.
        var fileSystem = services.GetService<IFileSystem>() ?? new DefaultFileSystem();
        var diskProbe = services.GetService<IDiskSpaceProbe>() ?? new DefaultDiskSpaceProbe();
        var gitHubProbe = services.GetService<IGitHubReachabilityProbe>()
            ?? new HttpGitHubReachabilityProbe(BuildHttpClient(CliConfig.Load()));

        var root = new RootCommand("CLI wrapper around MITRE Synthea synthetic patient generator");

        var refreshOpt = new Option<bool>("--refresh")
        {
            Description = "Ignore cached JAR and download the newest release",
            Recursive = true
        };

        var javaOpt = new Option<string?>("--java-path")
        {
            Description = "Full path to the Java executable (defaults to 'java' on PATH)",
            Recursive = true
        };

        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Enable debug-level logging",
            Recursive = true
        };

        var quietOpt = new Option<bool>("--quiet")
        {
            Description = "Suppress info logs; show only warnings and errors",
            Recursive = true
        };

        var skipJdkCheckOpt = new Option<bool>("--skip-jdk-check")
        {
            Description = "Bypass the Java 17+ preflight check. Use only for custom JREs whose " +
                          "-version output we can't parse; otherwise fix Java first.",
            Recursive = true
        };

        root.Options.Add(refreshOpt);
        root.Options.Add(javaOpt);
        root.Options.Add(verboseOpt);
        root.Options.Add(quietOpt);
        root.Options.Add(skipJdkCheckOpt);

        root.Subcommands.Add(RunCommand.Build(runner, jarSource, javaDetector, refreshOpt, javaOpt, skipJdkCheckOpt));
        root.Subcommands.Add(CacheCommand.Build(jarSource));
        root.Subcommands.Add(DoctorCommand.Build(jarSource, javaDetector, fileSystem, gitHubProbe, diskProbe, javaOpt));
        root.Subcommands.Add(ModulesCommand.Build(jarSource));

        // D1: intercept --version before the framework's stock handler so we
        // can render a two-line report including the cached JAR's version
        // and date. Only short-circuits the bare `synthea --version` form;
        // `synthea --version --help` still falls through to --help.
        if (args.Length > 0 && args[0] == "--version")
        {
            Console.WriteLine(BuildVersionReport(jarSource));
            return 0;
        }

        if (args.Length == 0) args = new[] { "--help" };
        return await root.Parse(args).InvokeAsync();
    }

    // D1: returns the multi-line version report. Public + ServiceLocator-
    // free for testability — pass any IJarSource (real or stubbed).
    internal static string BuildVersionReport(IJarSource jarSource)
        => BuildVersionReport(jarSource, RunCommand.ResolveOverrideJar(),
                              Environment.GetEnvironmentVariable("SYNTHEA_JAR_VERSION"));

    // overrideJar: an explicit JAR from --jar/SYNTHEA_CLI_JAR_PATH/config that
    // takes precedence over the download cache (so a baked JAR is reported, not
    // "not yet cached"). bakedSyntheaVersion: the Synthea release stamped into a
    // container image (env SYNTHEA_JAR_VERSION) — authoritative when present,
    // because Synthea's JAR carries no clean Implementation-Version. Separate
    // overload keeps the report unit-testable without touching process env.
    internal static string BuildVersionReport(IJarSource jarSource, FileInfo? overrideJar, string? bakedSyntheaVersion = null)
    {
        var sb = new StringBuilder();
        sb.Append("synthea-cli ").Append(GetCliVersion()).AppendLine();
        var jar = overrideJar ?? SafeFindCachedJar(jarSource);
        if (jar is null)
        {
            sb.Append("synthea-jar (not yet cached — run `synthea run` once to download)");
        }
        else
        {
            var jarVer = (!string.IsNullOrEmpty(bakedSyntheaVersion) && bakedSyntheaVersion != "latest")
                ? bakedSyntheaVersion
                : (TryReadJarVersion(jar.FullName) ?? "version unavailable");
            sb.Append("synthea-jar ").Append(jarVer);
            if (overrideJar is not null)
                sb.Append(" (configured: ").Append(jar.FullName).Append(')');
            else
                sb.Append(" (cached ").Append(jar.LastWriteTimeUtc.ToString("yyyy-MM-dd")).Append(')');
        }
        return sb.ToString();
    }

    private static FileInfo? SafeFindCachedJar(IJarSource jarSource)
    {
        try { return jarSource.TryFindCachedJar(); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    internal static string GetCliVersion()
    {
        var asm = typeof(Program).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info ?? asm.GetName().Version?.ToString() ?? "unknown";
    }

    // Cheap MANIFEST.MF read. Synthea's JAR puts Implementation-Version in
    // META-INF/MANIFEST.MF; if not present, fall back to a filename-shape
    // guess (e.g. synthea-3.3.0-with-dependencies.jar). Returns null if we
    // can't determine anything.
    internal static string? TryReadJarVersion(string jarPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(jarPath);
            var manifest = archive.GetEntry("META-INF/MANIFEST.MF");
            if (manifest is not null)
            {
                using var stream = manifest.Open();
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (line.StartsWith("Implementation-Version:", StringComparison.OrdinalIgnoreCase))
                        return line.Substring("Implementation-Version:".Length).Trim();
                }
            }
        }
        catch (IOException) { /* fall through */ }
        catch (InvalidDataException) { /* not a valid zip */ }

        // Filename fallback: synthea-X.Y.Z-with-dependencies.jar
        var name = Path.GetFileName(jarPath);
        var match = System.Text.RegularExpressions.Regex.Match(
            name, @"synthea-(?<v>\d+\.\d+\.\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["v"].Value : null;
    }

    internal static LogLevel DetectLogLevel(string[] args)
    {
        // --verbose wins over --quiet if both are passed, matching the
        // precedence most CLIs (curl, ssh) use: explicit verbosity beats
        // explicit silence so a debugging run never accidentally hides logs.
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--verbose") return LogLevel.Debug;
        }
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--quiet") return LogLevel.Warning;
        }
        return LogLevel.Information;
    }
}
