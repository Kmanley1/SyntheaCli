using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Synthea.Cli;

internal static class RunCommand
{
    private static readonly HashSet<string> AllowedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "fhir", "csv", "ccda", "bulkfhir", "bulk-fhir", "cpcds"
    };

    internal static Command Build(Option<bool> refreshOpt, Option<string?> javaOpt)
    {
        var runCmd = new Command("run", "Generate synthetic health records");

        var outputOpt = new Option<DirectoryInfo>(
            aliases: new[] { "--output", "-o" },
            description: "Directory where Synthea will write its output")
        { IsRequired = true };

        var stateOpt = CreateStateOption();
        var cityOpt = CreateCityOption();
        var genderOpt = CreateGenderOption();
        var ageOpt = CreateAgeRangeOption();
        var moduleDirOpt = CreateModuleDirOption();
        var moduleOpt = CreateModuleOption();
        var popOpt = CreatePopulationOption();
        var seedOpt = CreateSeedOption();
        var configOpt = CreateConfigOption();
        var zipOpt = CreateZipOption();
        var fhirVerOpt = CreateFhirVersionOption();
        var initialSnapOpt = CreateInitialSnapshotOption();
        var updatedSnapOpt = CreateUpdatedSnapshotOption();
        var daysForwardOpt = CreateDaysForwardOption();
        var formatOpt = CreateFormatOption();
        var printArgsOpt = new Option<bool>(
            "--print-args",
            "Print the java command line that would be run, then exit without running it. " +
            "Useful for debugging or scripting. Does not download the JAR.");
        var passthru = CreatePassthruArgument();

        runCmd.AddOption(outputOpt);
        runCmd.AddOption(stateOpt);
        runCmd.AddOption(cityOpt);
        runCmd.AddOption(genderOpt);
        runCmd.AddOption(ageOpt);
        runCmd.AddOption(moduleDirOpt);
        runCmd.AddOption(moduleOpt);
        runCmd.AddOption(popOpt);
        runCmd.AddOption(seedOpt);
        runCmd.AddOption(configOpt);
        runCmd.AddOption(zipOpt);
        runCmd.AddOption(fhirVerOpt);
        runCmd.AddOption(initialSnapOpt);
        runCmd.AddOption(updatedSnapOpt);
        runCmd.AddOption(daysForwardOpt);
        runCmd.AddOption(formatOpt);
        runCmd.AddOption(printArgsOpt);
        runCmd.AddArgument(passthru);

        runCmd.TreatUnmatchedTokensAsErrors = false;

        runCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var opts = ParseRunOptions(ctx, refreshOpt, javaOpt, outputOpt, stateOpt, cityOpt,
                genderOpt, ageOpt, moduleDirOpt, moduleOpt, popOpt, seedOpt, configOpt, zipOpt,
                fhirVerOpt, initialSnapOpt, updatedSnapOpt, daysForwardOpt, formatOpt, passthru);
            var printArgs = ctx.ParseResult.GetValueForOption(printArgsOpt);
            var cancelToken = ctx.GetCancellationToken();

            if (!string.IsNullOrWhiteSpace(opts.City) && string.IsNullOrWhiteSpace(opts.State))
            {
                Console.Error.WriteLine("--city requires --state to be specified.");
                ctx.ExitCode = 1;
                return;
            }
            if (!string.IsNullOrWhiteSpace(opts.Zip) && string.IsNullOrWhiteSpace(opts.State))
            {
                Console.Error.WriteLine("--zip requires --state to be specified.");
                ctx.ExitCode = 1;
                return;
            }

            if (printArgs)
            {
                ctx.ExitCode = PrintInvocation(opts);
                return;
            }

            Directory.CreateDirectory(opts.Output.FullName);

            try
            {
                var interactive = !Console.IsOutputRedirected;
                if (!interactive) Console.WriteLine("Downloading Synthea JAR...");
                var progress = new Progress<(long dl, long total)>(p =>
                {
                    if (interactive)
                        Console.Write($"\rDownloading Synthea {p.dl / 1_000_000}/{p.total / 1_000_000} MB…");
                });

                var jar = await Program.EnsureJarAsyncFunc(opts.Refresh, progress, cancelToken);

                if (interactive) Console.WriteLine();
                Console.WriteLine($"✓ Using {jar.Name}  ({jar.FullName})");

                var psi = CreateProcessStartInfo(opts, jar);

                using var proc = Program.Runner.Start(psi);
                using var killReg = cancelToken.Register(() =>
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
                });
                var pumpOut = Task.Run(() => ProcessHelpers.Relay(proc.StandardOutput, Console.Out));
                var pumpErr = Task.Run(() => ProcessHelpers.Relay(proc.StandardError, Console.Error));

                await Task.WhenAll(pumpOut, pumpErr, proc.WaitForExitAsync());
                ctx.ExitCode = proc.ExitCode;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                ctx.ExitCode = 130; // conventional SIGINT exit code
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Network error reaching GitHub: {ex.Message}");
                Console.Error.WriteLine("Check your connection, proxy, or GitHub API rate limits.");
                ctx.ExitCode = 3;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Synthea JAR error: {ex.Message}");
                ctx.ExitCode = 3;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Filesystem error: {ex.Message}");
                ctx.ExitCode = 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error ({ex.GetType().Name}): {ex.Message}");
                ctx.ExitCode = 4;
            }
        });

        return runCmd;
    }

    internal static List<string> BuildArgumentList(RunOptions o)
    {
        var argList = new List<string>();
        if (o.Population.HasValue)
        {
            argList.Add("-p");
            argList.Add(o.Population.Value.ToString());
        }
        if (o.Seed.HasValue)
        {
            argList.Add("-s");
            argList.Add(o.Seed.Value.ToString());
        }
        if (o.Config is not null)
        {
            argList.Add("-c");
            argList.Add(o.Config.FullName);
        }
        if (!string.IsNullOrWhiteSpace(o.Gender))
        {
            argList.Add("--gender");
            argList.Add(o.Gender!.ToUpperInvariant());
        }
        if (!string.IsNullOrWhiteSpace(o.AgeRange))
        {
            argList.Add("--age-range");
            argList.Add(o.AgeRange!);
        }
        if (o.ModuleDir is not null)
        {
            argList.Add("--module-dir");
            argList.Add(o.ModuleDir.FullName);
        }
        if (o.Modules is not null)
        {
            foreach (var m in o.Modules)
            {
                argList.Add("--module");
                argList.Add(m);
            }
        }
        if (o.InitialSnapshot is not null)
        {
            argList.Add("-i");
            argList.Add(o.InitialSnapshot.FullName);
        }
        if (o.UpdatedSnapshot is not null)
        {
            argList.Add("-u");
            argList.Add(o.UpdatedSnapshot.FullName);
        }
        if (o.DaysForward.HasValue)
        {
            argList.Add("-t");
            argList.Add(o.DaysForward.Value.ToString());
        }
        if (!string.IsNullOrWhiteSpace(o.FhirVersion))
        {
            argList.Add("--exporter.fhir.version=" + o.FhirVersion!.ToUpperInvariant());
        }
        if (o.Formats.Length > 0)
        {
            var set = new HashSet<string>(o.Formats.Select(f => f.ToLowerInvariant()));
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
                var enable = set.Contains(kv.Key) ||
                             (kv.Key == "bulkfhir" && (set.Contains("bulk-fhir") || set.Contains("bulkfhir")));
                argList.Add("--" + kv.Value + "=" + (enable ? "true" : "false"));
            }
        }
        argList.AddRange(o.Passthru);
        if (!string.IsNullOrWhiteSpace(o.State)) argList.Add(o.State);
        if (!string.IsNullOrWhiteSpace(o.City)) argList.Add(o.City);
        if (!string.IsNullOrWhiteSpace(o.Zip)) argList.Add(o.Zip);

        return argList;
    }

    internal static ProcessStartInfo CreateProcessStartInfo(RunOptions o, FileInfo jar)
    {
        var psi = new ProcessStartInfo(o.JavaPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = o.Output.FullName
        };
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(jar.FullName);
        foreach (var a in BuildArgumentList(o))
            psi.ArgumentList.Add(a);
        return psi;
    }

    private static int PrintInvocation(RunOptions o)
    {
        var cachedJar = JarManager.TryFindCachedJar();
        var jarLabel = cachedJar?.FullName
            ?? "<synthea.jar — not yet cached; run once without --print-args>";
        Console.WriteLine($"# Java executable: {o.JavaPath}");
        Console.WriteLine($"# Synthea JAR:     {jarLabel}");
        Console.WriteLine($"# Working dir:     {o.Output.FullName}");
        Console.Write(QuoteForShell(o.JavaPath));
        Console.Write(" -jar ");
        Console.Write(QuoteForShell(jarLabel));
        foreach (var a in BuildArgumentList(o))
        {
            Console.Write(' ');
            Console.Write(QuoteForShell(a));
        }
        Console.WriteLine();
        return 0;
    }

    private static string QuoteForShell(string s)
    {
        if (s.Length == 0) return "\"\"";
        if (s.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return s;
        return "\"" + s.Replace("\"", "\\\"") + "\"";
    }

    private static RunOptions ParseRunOptions(InvocationContext ctx,
        Option<bool> refreshOpt,
        Option<string?> javaOpt,
        Option<DirectoryInfo> outputOpt,
        Option<string?> stateOpt,
        Option<string?> cityOpt,
        Option<string?> genderOpt,
        Option<string?> ageOpt,
        Option<DirectoryInfo?> moduleDirOpt,
        Option<string[]> moduleOpt,
        Option<int?> popOpt,
        Option<int?> seedOpt,
        Option<FileInfo?> configOpt,
        Option<string?> zipOpt,
        Option<string?> fhirOpt,
        Option<FileInfo?> initSnapOpt,
        Option<FileInfo?> updSnapOpt,
        Option<int?> daysOpt,
        Option<string[]> formatOpt,
        Argument<string[]> passthru)
    {
        var javaPathArg = ctx.ParseResult.GetValueForOption(javaOpt);
        return new RunOptions(
            Output: ctx.ParseResult.GetValueForOption(outputOpt)!,
            Refresh: ctx.ParseResult.GetValueForOption(refreshOpt),
            JavaPath: string.IsNullOrWhiteSpace(javaPathArg) ? "java" : javaPathArg!,
            State: ctx.ParseResult.GetValueForOption(stateOpt),
            City: ctx.ParseResult.GetValueForOption(cityOpt),
            Gender: ctx.ParseResult.GetValueForOption(genderOpt),
            AgeRange: ctx.ParseResult.GetValueForOption(ageOpt),
            ModuleDir: ctx.ParseResult.GetValueForOption(moduleDirOpt),
            Modules: ctx.ParseResult.GetValueForOption(moduleOpt),
            Population: ctx.ParseResult.GetValueForOption(popOpt),
            Seed: ctx.ParseResult.GetValueForOption(seedOpt),
            Config: ctx.ParseResult.GetValueForOption(configOpt),
            Zip: ctx.ParseResult.GetValueForOption(zipOpt),
            FhirVersion: ctx.ParseResult.GetValueForOption(fhirOpt),
            InitialSnapshot: ctx.ParseResult.GetValueForOption(initSnapOpt),
            UpdatedSnapshot: ctx.ParseResult.GetValueForOption(updSnapOpt),
            DaysForward: ctx.ParseResult.GetValueForOption(daysOpt),
            Formats: ctx.ParseResult.GetValueForOption(formatOpt) ?? Array.Empty<string>(),
            Passthru: ctx.ParseResult.GetValueForArgument(passthru));
    }

    // ----- Option-validator helpers ---------------------------------------
    //
    // Every Create*Option method below shares the same pattern: "if a value
    // was supplied, run a check and set r.ErrorMessage when it fails." Two
    // helpers fold that pattern away so each option declaration shows only
    // the rule itself, not the boilerplate around it.

    private static ValidateSymbolResult<OptionResult> SingleTokenValidator(Func<string, string?> check) => r =>
    {
        if (r.Tokens.Count == 0) return;
        var err = check(r.Tokens[0].Value);
        if (err is not null) r.ErrorMessage = err;
    };

    private static ValidateSymbolResult<OptionResult> MultiTokenValidator(Func<string, string?> check) => r =>
    {
        foreach (var t in r.Tokens)
        {
            var err = check(t.Value);
            if (err is not null) { r.ErrorMessage = err; return; }
        }
    };

    // ----- Per-option factories -------------------------------------------

    private static Option<string?> CreateStateOption()
    {
        // Format-only check (two letters). The previous 50-entry US-state
        // allowlist silently rejected DC, PR, GU, VI, and any future
        // Synthea-supported geo codes. Defer the "is this a real place?"
        // check to Synthea itself, which owns the geo data.
        var opt = new Option<string?>("--state",
            "Two-letter state/territory code (e.g. OH, TX, DC, PR). Format-only check; Synthea rejects unknown codes itself.");
        opt.AddValidator(SingleTokenValidator(v =>
            v.Length == 2 && v.All(char.IsLetter) ? null : "State code must be exactly two letters."));
        return opt;
    }

    private static Option<string?> CreateCityOption()
    {
        var opt = new Option<string?>("--city", "City name (optional second positional arg after state)");
        opt.AddValidator(SingleTokenValidator(v =>
            string.IsNullOrWhiteSpace(v) ? "City name cannot be empty." : null));
        return opt;
    }

    private static Option<string?> CreateGenderOption()
    {
        var opt = new Option<string?>("--gender", "Patient gender filter (M or F)");
        opt.AddValidator(SingleTokenValidator(v =>
        {
            var g = v.ToUpperInvariant();
            return g == "M" || g == "F" ? null : "Gender must be 'M' or 'F'.";
        }));
        return opt;
    }

    private static Option<string?> CreateAgeRangeOption()
    {
        var opt = new Option<string?>("--age-range", "Age range filter as min-max");
        opt.AddValidator(SingleTokenValidator(v =>
        {
            var parts = v.Split('-', 2);
            return parts.Length == 2
                   && int.TryParse(parts[0], out var min)
                   && int.TryParse(parts[1], out var max)
                   && min >= 0 && max >= min
                ? null
                : "Age range must be 'min-max' with min <= max.";
        }));
        return opt;
    }

    private static Option<DirectoryInfo?> CreateModuleDirOption()
    {
        var opt = new Option<DirectoryInfo?>("--module-dir", "Directory of custom modules");
        opt.AddValidator(SingleTokenValidator(v =>
            Directory.Exists(v) ? null : "Module directory does not exist."));
        return opt;
    }

    private static Option<string[]> CreateModuleOption()
    {
        var opt = new Option<string[]>("--module", "Specific disease modules") { Arity = ArgumentArity.ZeroOrMore };
        opt.AddValidator(MultiTokenValidator(v =>
            string.IsNullOrWhiteSpace(v) ? "Module name cannot be empty." : null));
        return opt;
    }

    private static Option<int?> CreatePopulationOption()
    {
        var opt = new Option<int?>(aliases: new[] { "--population", "-p" }, description: "Number of patients to generate");
        opt.AddValidator(SingleTokenValidator(v =>
            int.TryParse(v, out var n) && n > 0 ? null : "Population must be a positive integer."));
        return opt;
    }

    private static Option<int?> CreateSeedOption()
    {
        var opt = new Option<int?>(aliases: new[] { "--seed", "-s" }, description: "Random seed for deterministic output");
        opt.AddValidator(SingleTokenValidator(v =>
            int.TryParse(v, out _) ? null : "Random seed must be an integer."));
        return opt;
    }

    private static Option<FileInfo?> CreateConfigOption()
    {
        var opt = new Option<FileInfo?>(aliases: new[] { "--config", "-c" }, description: "Path to Synthea configuration file");
        opt.AddValidator(SingleTokenValidator(v =>
            File.Exists(v) ? null : "Configuration file does not exist."));
        return opt;
    }

    private static Option<string?> CreateZipOption()
    {
        var opt = new Option<string?>("--zip", "ZIP code (requires --state)");
        opt.AddValidator(SingleTokenValidator(v =>
            Regex.IsMatch(v, @"^\d{5}(?:-\d{4})?$") ? null : "ZIP code must be 5 digits or 5+4."));
        return opt;
    }

    private static Option<string?> CreateFhirVersionOption()
    {
        var opt = new Option<string?>("--fhir-version", "FHIR version (R4 or STU3)");
        opt.AddValidator(SingleTokenValidator(v =>
        {
            var u = v.ToUpperInvariant();
            return u == "R4" || u == "STU3" ? null : "FHIR version must be R4 or STU3.";
        }));
        return opt;
    }

    private static Option<FileInfo?> CreateInitialSnapshotOption()
    {
        var opt = new Option<FileInfo?>("--initial-snapshot", "Path to initial snapshot to load (-i)");
        opt.AddValidator(SingleTokenValidator(v =>
            File.Exists(v) ? null : "Initial snapshot file does not exist."));
        return opt;
    }

    private static Option<FileInfo?> CreateUpdatedSnapshotOption()
    {
        var opt = new Option<FileInfo?>("--updated-snapshot", "Path where updated snapshot will be written (-u)");
        opt.AddValidator(SingleTokenValidator(v =>
        {
            var dir = Path.GetDirectoryName(v);
            return !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)
                ? null
                : "Directory for updated snapshot must exist.";
        }));
        return opt;
    }

    private static Option<int?> CreateDaysForwardOption()
    {
        var opt = new Option<int?>("--days-forward", "Advance time from snapshot by N days (-t)");
        opt.AddValidator(SingleTokenValidator(v =>
            int.TryParse(v, out var n) && n > 0 ? null : "Days forward must be a positive integer."));
        return opt;
    }

    private static Option<string[]> CreateFormatOption()
    {
        var opt = new Option<string[]>("--format",
            "Output formats to generate (FHIR, CSV, CCDA, BULKFHIR, CPCDS)")
        { Arity = ArgumentArity.ZeroOrMore };
        opt.AddValidator(MultiTokenValidator(v =>
            AllowedFormats.Contains(v) ? null : $"Unsupported format '{v}'."));
        return opt;
    }

    private static Argument<string[]> CreatePassthruArgument() => new("args")
    {
        Arity = ArgumentArity.ZeroOrMore,
        Description = "Any other arguments forwarded unchanged to synthea.jar"
    };
}
