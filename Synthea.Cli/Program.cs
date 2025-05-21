// Program.cs  – entry point for synthea-cli
// namespace: Synthea.Cli

using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Collections.Generic;
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
        public void Dispose() => _proc.Dispose();
    }
}

internal static class Program
{
    internal static IProcessRunner Runner { get; set; } = new DefaultProcessRunner();
    internal static Func<bool, IProgress<(long, long)>?, CancellationToken, Task<FileInfo>> EnsureJarAsyncFunc { get; set; } = JarManager.EnsureJarAsync;
    private static readonly HashSet<string> ValidStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA","HI","ID","IL","IN","IA","KS",
        "KY","LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ","NM","NY",
        "NC","ND","OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA","WA","WV","WI","WY"
    };
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
        stateOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var v = r.Tokens[0].Value.ToUpperInvariant();
            if (v.Length != 2 || !ValidStates.Contains(v))
                r.ErrorMessage = "State must be a valid two letter code.";
        });

        var cityOpt = new Option<string?>(
            "--city",
            description: "City name (optional second positional arg after state)");
        cityOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (string.IsNullOrWhiteSpace(r.Tokens[0].Value))
                r.ErrorMessage = "City name cannot be empty.";
        });

        var genderOpt = new Option<string?>("--gender", "Patient gender filter (M or F)");
        genderOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var g = r.Tokens[0].Value.ToUpperInvariant();
            if (g != "M" && g != "F")
                r.ErrorMessage = "Gender must be 'M' or 'F'.";
        });

        var ageOpt = new Option<string?>("--age-range", "Age range filter as min-max");
        ageOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var parts = r.Tokens[0].Value.Split('-', 2);
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var min) ||
                !int.TryParse(parts[1], out var max) ||
                min < 0 || max < min)
            {
                r.ErrorMessage = "Age range must be 'min-max' with min <= max.";
            }
        });

        var moduleDirOpt = new Option<DirectoryInfo?>("--module-dir", "Directory of custom modules");
        moduleDirOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!Directory.Exists(r.Tokens[0].Value))
                r.ErrorMessage = "Module directory does not exist.";
        });

        var moduleOpt = new Option<string[]>("--module", "Specific disease modules")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        moduleOpt.AddValidator(r =>
        {
            foreach (var t in r.Tokens)
            {
                if (string.IsNullOrWhiteSpace(t.Value))
                {
                    r.ErrorMessage = "Module name cannot be empty.";
                    return;
                }
            }
        });

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

        var initialSnapOpt = new Option<FileInfo?>("--initial-snapshot", "Path to initial snapshot to load (-i)");
        initialSnapOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!File.Exists(r.Tokens[0].Value))
                r.ErrorMessage = "Initial snapshot file does not exist.";
        });

        var updatedSnapOpt = new Option<FileInfo?>("--updated-snapshot", "Path where updated snapshot will be written (-u)");
        updatedSnapOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var dir = Path.GetDirectoryName(r.Tokens[0].Value);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                r.ErrorMessage = "Directory for updated snapshot must exist.";
        });

        var daysForwardOpt = new Option<int?>("--days-forward", "Advance time from snapshot by N days (-t)");
        daysForwardOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!int.TryParse(r.Tokens[0].Value, out var v) || v <= 0)
                r.ErrorMessage = "Days forward must be a positive integer.";
        });

        var formatOpt = new Option<string[]>("--format", "Output formats to generate (FHIR, CSV, CCDA, BULKFHIR, CPCDS)")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        formatOpt.AddValidator(r =>
        {
            var allowed = new[] { "fhir", "csv", "ccda", "bulkfhir", "bulk-fhir", "cpcds" };
            foreach (var t in r.Tokens)
            {
                var norm = t.Value.ToLowerInvariant();
                if (!allowed.Contains(norm))
                {
                    r.ErrorMessage = $"Unsupported format '{t.Value}'.";
                    return;
                }
            }
        });

        // capture *any* additional Synthea flags
        var passthru = new Argument<string[]>("args")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Any other arguments forwarded unchanged to synthea.jar"
        };

        runCmd.AddOption(outputOpt);
        runCmd.AddOption(stateOpt);
        runCmd.AddOption(cityOpt);
        runCmd.AddOption(genderOpt);
        runCmd.AddOption(ageOpt);
        runCmd.AddOption(moduleDirOpt);
        runCmd.AddOption(moduleOpt);
        runCmd.AddOption(popOpt);
        runCmd.AddOption(seedOpt);
        runCmd.AddOption(initialSnapOpt);
        runCmd.AddOption(updatedSnapOpt);
        runCmd.AddOption(daysForwardOpt);
        runCmd.AddOption(formatOpt);
        runCmd.AddArgument(passthru);
        // Forward any unrecognized options directly to Synthea
        runCmd.TreatUnmatchedTokensAsErrors = false;

        runCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var refresh = ctx.ParseResult.GetValueForOption(refreshOpt);
            var javaPathArg = ctx.ParseResult.GetValueForOption(javaOpt);
            var javaPath = !string.IsNullOrWhiteSpace(javaPathArg) ? javaPathArg : "java";
            var outDir = ctx.ParseResult.GetValueForOption(outputOpt)!;
            var state = ctx.ParseResult.GetValueForOption(stateOpt);
            var city = ctx.ParseResult.GetValueForOption(cityOpt);
            var gender = ctx.ParseResult.GetValueForOption(genderOpt);
            var age = ctx.ParseResult.GetValueForOption(ageOpt);
            var moduleDir = ctx.ParseResult.GetValueForOption(moduleDirOpt);
            var modules = ctx.ParseResult.GetValueForOption(moduleOpt);
            var pop = ctx.ParseResult.GetValueForOption(popOpt);
            var seed = ctx.ParseResult.GetValueForOption(seedOpt);
            var initSnap = ctx.ParseResult.GetValueForOption(initialSnapOpt);
            var updSnap = ctx.ParseResult.GetValueForOption(updatedSnapOpt);
            var daysFwd = ctx.ParseResult.GetValueForOption(daysForwardOpt);
            var formats = ctx.ParseResult.GetValueForOption(formatOpt) ?? Array.Empty<string>();
            var rest = ctx.ParseResult.GetValueForArgument(passthru);

            Directory.CreateDirectory(outDir.FullName);

            if (!string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(state))
            {
                Console.Error.WriteLine("--city requires --state to be specified.");
                ctx.ExitCode = 1;
                return;
            }

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
            if (!string.IsNullOrWhiteSpace(gender))
            {
                argList.Add("--gender");
                argList.Add(gender.ToUpperInvariant());
            }
            if (!string.IsNullOrWhiteSpace(age))
            {
                argList.Add("--age-range");
                argList.Add(age);
            }
            if (moduleDir is not null)
            {
                argList.Add("--module-dir");
                argList.Add(moduleDir.FullName);
            }
            if (modules is not null)
            {
                foreach (var m in modules)
                {
                    argList.Add("--module");
                    argList.Add(m);
                }
            }
            if (initSnap is not null)
            {
                argList.Add("-i");
                argList.Add(initSnap.FullName);
            }
            if (updSnap is not null)
            {
                argList.Add("-u");
                argList.Add(updSnap.FullName);
            }
            if (daysFwd.HasValue)
            {
                argList.Add("-t");
                argList.Add(daysFwd.Value.ToString());
            }
            if (formats.Length > 0)
            {
                var set = new HashSet<string>(formats.Select(f => f.ToLowerInvariant()));
                var map = new Dictionary<string, string>
                {
                    ["fhir"] = "exporter.fhir.export",
                    ["csv"] = "exporter.csv.export",
                    ["ccda"] = "exporter.ccda.export",
                    ["bulkfhir"] = "exporter.fhir.bulk_data",
                    ["cpcds"] = "exporter.cpcds.export"
                };
                foreach (var kv in map)
                {
                    var enable = set.Contains(kv.Key) || (kv.Key == "bulkfhir" && (set.Contains("bulk-fhir") || set.Contains("bulkfhir")));
                    argList.Add("--" + kv.Value + "=" + (enable ? "true" : "false"));
                }
            }
            argList.AddRange(rest);
            if (!string.IsNullOrWhiteSpace(state)) argList.Add(state);
            if (!string.IsNullOrWhiteSpace(city)) argList.Add(city);

            var psi = new ProcessStartInfo(javaPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = outDir.FullName
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
