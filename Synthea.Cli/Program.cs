// Program.cs  – entry point for synthea-cli
// namespace: Synthea.Cli

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

namespace Synthea.Cli;

internal interface IProcessRunner
{
    IProcess Start(ProcessStartInfo psi);
}

internal interface IProcess : IDisposable
{
    StreamReader StandardOutput { get; }
    StreamReader StandardError  { get; }
    Task WaitForExitAsync();
    int ExitCode { get; }
}

internal sealed class DefaultProcessRunner : IProcessRunner
{
    public IProcess Start(ProcessStartInfo psi) => new ProcessWrapper(Process.Start(psi)!);

    private sealed class ProcessWrapper : IProcess
    {
        private readonly Process _proc;
        public ProcessWrapper(Process proc) => _proc = proc;
        public StreamReader StandardOutput => _proc.StandardOutput;
        public StreamReader StandardError  => _proc.StandardError;
        public Task WaitForExitAsync() => _proc.WaitForExitAsync();
        public int ExitCode => _proc.ExitCode;
        public void Dispose() => _proc.Dispose();
    }
}

internal static class Program
{
    internal static IProcessRunner Runner { get; set; } = new DefaultProcessRunner();
    internal static Func<bool, IProgress<(long, long)>?, CancellationToken, Task<FileInfo>> EnsureJarAsyncFunc { get; set; } = JarManager.EnsureJarAsync;
    public static async Task<int> Main(string[] args)
    {
        // ───── root command & global options ─────
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

        // ───── run sub-command ─────
        var runCmd = new Command("run", "Generate synthetic health records");

        var outputOpt = new Option<DirectoryInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Directory where Synthea will write its output")
        { IsRequired = true };

        var stateOpt = new Option<string?>(
            "--state",
            description: "Two-letter state code (e.g. OH, TX). Adds it as positional state arg");

        var cityOpt = new Option<string?>(
            "--city",
            description: "City name (optional second positional arg after state)");

        var popOpt = new Option<int?>(
            aliases: new[] { "--population", "-p" },
            description: "Number of patients to generate");
        popOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!int.TryParse(r.Tokens[0].Value, out var v) || v <= 0)
                r.ErrorMessage = "Population must be a positive integer.";
        });

        var seedOpt = new Option<int?>(
            aliases: new[] { "--seed", "-s" },
            description: "Random seed for deterministic output");
        seedOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!int.TryParse(r.Tokens[0].Value, out _))
                r.ErrorMessage = "Random seed must be an integer.";
        });

        // capture *any* additional Synthea flags
        var passthru = new Argument<string[]>("args")
        {
            Arity       = ArgumentArity.ZeroOrMore,
            Description = "Any other arguments forwarded unchanged to synthea.jar"
        };

        runCmd.AddOption(outputOpt);
        runCmd.AddOption(stateOpt);
        runCmd.AddOption(cityOpt);
        runCmd.AddOption(popOpt);
        runCmd.AddOption(seedOpt);
        runCmd.AddArgument(passthru);
        // Forward any unrecognized options directly to Synthea
        runCmd.TreatUnmatchedTokensAsErrors = false;

        runCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var refresh      = ctx.ParseResult.GetValueForOption(refreshOpt);
            var javaPathArg  = ctx.ParseResult.GetValueForOption(javaOpt);
            var javaPath     = !string.IsNullOrWhiteSpace(javaPathArg) ? javaPathArg : "java";
            var outDir    = ctx.ParseResult.GetValueForOption(outputOpt)!;
            var state     = ctx.ParseResult.GetValueForOption(stateOpt);
            var city      = ctx.ParseResult.GetValueForOption(cityOpt);
            var pop       = ctx.ParseResult.GetValueForOption(popOpt);
            var seed      = ctx.ParseResult.GetValueForOption(seedOpt);
            var rest      = ctx.ParseResult.GetValueForArgument(passthru);

            Directory.CreateDirectory(outDir.FullName);

            // download or reuse JAR
            var jar = await EnsureJarAsyncFunc(
                          refresh,
                          new Progress<(long dl, long total)>(p =>
                              Console.Write($"\rDownloading Synthea {p.dl / 1_000_000}/{p.total / 1_000_000} MB…")),
                          ctx.GetCancellationToken());

            Console.WriteLine($"\n✓ Using {jar.Name}");

            // build complete arg list: user flags first, then state/city
            var argList = new List<string>();
            if (pop.HasValue)
            {
                argList.Add("-p");
                argList.Add(pop.Value.ToString());
            }
            if (seed.HasValue)
            {
                argList.Add("-s");
                argList.Add(seed.Value.ToString());
            }
            argList.AddRange(rest);
            if (!string.IsNullOrWhiteSpace(state)) argList.Add(state);
            if (!string.IsNullOrWhiteSpace(city))  argList.Add(city);

            var psi = new ProcessStartInfo(javaPath)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                WorkingDirectory       = outDir.FullName
            };
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(jar.FullName);
            foreach (var a in argList) psi.ArgumentList.Add(a);

            // launch & relay output
            using var proc = Runner.Start(psi);
            var pumpOut = Task.Run(() => Relay(proc.StandardOutput, Console.Out));
            var pumpErr = Task.Run(() => Relay(proc.StandardError, Console.Error));

            await Task.WhenAll(pumpOut, pumpErr, proc.WaitForExitAsync());
            ctx.ExitCode = proc.ExitCode;
        });

        root.AddCommand(runCmd);

        // default to help when no args
        if (args.Length == 0) args = new[] { "--help" };
        return await root.InvokeAsync(args);
    }

    private static async Task Relay(StreamReader src, TextWriter dest)
    {
        string? line;
        while ((line = await src.ReadLineAsync()) is not null)
            dest.WriteLine(line);
    }
}
