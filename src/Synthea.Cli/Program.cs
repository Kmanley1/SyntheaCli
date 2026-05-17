using System;
using System.CommandLine;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        sc.AddLogging(b => b
            .SetMinimumLevel(minimumLevel)
            .AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            }));
        sc.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        sc.AddSingleton<IJavaDetector, JavaDetector>();
        sc.AddSingleton<IJarSource>(sp => new JarManager(
            http: BuildHttpClient(CliConfig.Load()),
            logger: sp.GetRequiredService<ILogger<JarManager>>()));
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
        client.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Synthea.Cli/0.1"));
        return client;
    }

    internal static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var runner = services.GetRequiredService<IProcessRunner>();
        var jarSource = services.GetRequiredService<IJarSource>();
        var javaDetector = services.GetRequiredService<IJavaDetector>();

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

        if (args.Length == 0) args = new[] { "--help" };
        return await root.Parse(args).InvokeAsync();
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
