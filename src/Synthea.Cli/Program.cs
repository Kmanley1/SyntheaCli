using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Synthea.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Synthea emits patient names with diacritics; force UTF-8 so they don't
        // mojibake on Windows code pages (cp437/cp1252).
        Console.OutputEncoding = Encoding.UTF8;

        await using var services = BuildDefaultServices();
        return await RunAsync(args, services);
    }

    internal static ServiceProvider BuildDefaultServices()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<IProcessRunner, DefaultProcessRunner>();
        sc.AddSingleton<IJarSource>(_ => new JarManager());
        return sc.BuildServiceProvider();
    }

    internal static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var runner = services.GetRequiredService<IProcessRunner>();
        var jarSource = services.GetRequiredService<IJarSource>();

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

        root.Options.Add(refreshOpt);
        root.Options.Add(javaOpt);

        root.Subcommands.Add(RunCommand.Build(runner, jarSource, refreshOpt, javaOpt));

        if (args.Length == 0) args = new[] { "--help" };
        return await root.Parse(args).InvokeAsync();
    }
}
