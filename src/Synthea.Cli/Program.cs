using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Synthea.Cli;

internal static class Program
{
    internal static IProcessRunner Runner { get; set; } = new DefaultProcessRunner();
    internal static Func<bool, IProgress<(long, long)>?, CancellationToken, Task<FileInfo>> EnsureJarAsyncFunc { get; set; } = JarManager.EnsureJarAsync;

    public static async Task<int> Main(string[] args)
    {
        // Synthea emits patient names with diacritics; force UTF-8 so they don't
        // mojibake on Windows code pages (cp437/cp1252).
        Console.OutputEncoding = Encoding.UTF8;

        var root = new RootCommand("CLI wrapper around MITRE Synthea synthetic patient generator");

        var refreshOpt = new Option<bool>(
            "--refresh",
            "Ignore cached JAR and download the newest release");

        var javaOpt = new Option<string?>(
            "--java-path",
            () => null,
            "Full path to the Java executable (defaults to 'java' on PATH)");

        root.AddGlobalOption(refreshOpt);
        root.AddGlobalOption(javaOpt);

        root.AddCommand(RunCommand.Build(refreshOpt, javaOpt));

        if (args.Length == 0) args = new[] { "--help" };
        return await root.InvokeAsync(args);
    }
}
