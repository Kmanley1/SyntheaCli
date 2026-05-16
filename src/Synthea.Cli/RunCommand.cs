using System;
using System.Collections.Generic;
using System.CommandLine;
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

    internal static Command Build(IProcessRunner runner, IJarSource jarSource, Option<bool> refreshOpt, Option<string?> javaOpt)
    {
        var runCmd = new Command("run", "Generate synthetic health records");

        var outputOpt = new Option<DirectoryInfo>("--output", "-o")
        {
            Description = "Directory where Synthea will write its output",
            Required = true
        };

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
        var addFormatOpt = CreateAddFormatOption();
        var printArgsOpt = new Option<bool>("--print-args")
        {
            Description = "Print the java command line that would be run, then exit without running it. " +
                          "Useful for debugging or scripting. Does not download the JAR."
        };
        var passthru = CreatePassthruArgument();

        runCmd.Options.Add(outputOpt);
        runCmd.Options.Add(stateOpt);
        runCmd.Options.Add(cityOpt);
        runCmd.Options.Add(genderOpt);
        runCmd.Options.Add(ageOpt);
        runCmd.Options.Add(moduleDirOpt);
        runCmd.Options.Add(moduleOpt);
        runCmd.Options.Add(popOpt);
        runCmd.Options.Add(seedOpt);
        runCmd.Options.Add(configOpt);
        runCmd.Options.Add(zipOpt);
        runCmd.Options.Add(fhirVerOpt);
        runCmd.Options.Add(initialSnapOpt);
        runCmd.Options.Add(updatedSnapOpt);
        runCmd.Options.Add(daysForwardOpt);
        runCmd.Options.Add(formatOpt);
        runCmd.Options.Add(addFormatOpt);
        runCmd.Options.Add(printArgsOpt);
        runCmd.Arguments.Add(passthru);

        runCmd.TreatUnmatchedTokensAsErrors = false;

        runCmd.SetAction(async (ParseResult parseResult, CancellationToken cancelToken) =>
        {
            var (hosting, args) = ParseRunOptions(parseResult, refreshOpt, javaOpt, outputOpt, stateOpt, cityOpt,
                genderOpt, ageOpt, moduleDirOpt, moduleOpt, popOpt, seedOpt, configOpt, zipOpt,
                fhirVerOpt, initialSnapOpt, updatedSnapOpt, daysForwardOpt, formatOpt, addFormatOpt, passthru);
            var printArgs = parseResult.GetValue(printArgsOpt);

            if (!string.IsNullOrWhiteSpace(args.City) && string.IsNullOrWhiteSpace(args.State))
            {
                Console.Error.WriteLine("--city requires --state to be specified.");
                return 1;
            }
            if (!string.IsNullOrWhiteSpace(args.Zip) && string.IsNullOrWhiteSpace(args.State))
            {
                Console.Error.WriteLine("--zip requires --state to be specified.");
                return 1;
            }

            if (printArgs)
            {
                return PrintInvocation(hosting, args, jarSource);
            }

            Directory.CreateDirectory(hosting.Output.FullName);

            try
            {
                var interactive = !Console.IsOutputRedirected;
                if (!interactive) Console.WriteLine("Downloading Synthea JAR...");
                var progress = new Progress<(long dl, long total)>(p =>
                {
                    if (interactive)
                        Console.Write($"\rDownloading Synthea {p.dl / 1_000_000}/{p.total / 1_000_000} MB…");
                });

                var jar = await jarSource.EnsureJarAsync(hosting.Refresh, progress, cancelToken);

                if (interactive) Console.WriteLine();
                Console.WriteLine($"✓ Using {jar.Name}  ({jar.FullName})");

                var psi = CreateProcessStartInfo(hosting, args, jar);

                using var proc = runner.Start(psi);
                using var killReg = cancelToken.Register(() =>
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
                });
                var pumpOut = Task.Run(() => ProcessHelpers.Relay(proc.StandardOutput, Console.Out));
                var pumpErr = Task.Run(() => ProcessHelpers.Relay(proc.StandardError, Console.Error));

                await Task.WhenAll(pumpOut, pumpErr, proc.WaitForExitAsync());
                return proc.ExitCode;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Cancelled.");
                return 130; // conventional SIGINT exit code
            }
            catch (HttpRequestException ex)
            {
                Console.Error.WriteLine($"Network error reaching GitHub: {ex.Message}");
                Console.Error.WriteLine("Check your connection, proxy, or GitHub API rate limits.");
                return 3;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Synthea JAR error: {ex.Message}");
                return 3;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine($"Filesystem error: {ex.Message}");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Unexpected error ({ex.GetType().Name}): {ex.Message}");
                return 4;
            }
        });

        return runCmd;
    }

    internal static List<string> BuildArgumentList(SyntheaArgs o)
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
        var formatPropertyMap = new Dictionary<string, string>
        {
            ["fhir"] = "exporter.fhir.export",
            ["csv"] = "exporter.csv.export",
            ["ccda"] = "exporter.ccda.export",
            ["bulkfhir"] = "exporter.fhir.bulk_data",
            ["bulk-fhir"] = "exporter.fhir.bulk_data",
            ["cpcds"] = "exporter.cpcds.export"
        };
        if (o.Formats.Length > 0)
        {
            // Exclusive: enable named formats, disable everything else.
            var set = new HashSet<string>(o.Formats.Select(f => f.ToLowerInvariant()));
            foreach (var kv in formatPropertyMap)
            {
                if (kv.Key == "bulk-fhir") continue; // alias; emitted via bulkfhir entry
                var enable = set.Contains(kv.Key) ||
                             (kv.Key == "bulkfhir" && (set.Contains("bulk-fhir") || set.Contains("bulkfhir")));
                argList.Add("--" + kv.Value + "=" + (enable ? "true" : "false"));
            }
        }
        foreach (var f in o.AdditionalFormats)
        {
            // Additive: enable each named format without disabling anything else.
            // Synthea evaluates --exporter.X.export=true|false in order, so an
            // additive "=true" after an exclusive "=false" wins.
            if (formatPropertyMap.TryGetValue(f.ToLowerInvariant(), out var prop))
                argList.Add("--" + prop + "=true");
        }
        // Positional state/city/zip must precede passthru tokens. Otherwise a
        // passthru flag that takes a value (e.g. `--some-flag`) could swallow
        // "OH" as its value before Synthea sees it as the state. (A-9)
        if (!string.IsNullOrWhiteSpace(o.State)) argList.Add(o.State);
        if (!string.IsNullOrWhiteSpace(o.City)) argList.Add(o.City);
        if (!string.IsNullOrWhiteSpace(o.Zip)) argList.Add(o.Zip);
        argList.AddRange(o.Passthru);

        return argList;
    }

    internal static ProcessStartInfo CreateProcessStartInfo(HostingOptions hosting, SyntheaArgs args, FileInfo jar)
    {
        var psi = new ProcessStartInfo(hosting.JavaPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = hosting.Output.FullName
        };
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(jar.FullName);
        foreach (var a in BuildArgumentList(args))
            psi.ArgumentList.Add(a);
        return psi;
    }

    private static int PrintInvocation(HostingOptions hosting, SyntheaArgs args, IJarSource jarSource)
    {
        var cachedJar = jarSource.TryFindCachedJar();
        var jarLabel = cachedJar?.FullName
            ?? "<synthea.jar — not yet cached; run once without --print-args>";
        Console.WriteLine($"# Java executable: {hosting.JavaPath}");
        Console.WriteLine($"# Synthea JAR:     {jarLabel}");
        Console.WriteLine($"# Working dir:     {hosting.Output.FullName}");
        Console.Write(QuoteForShell(hosting.JavaPath));
        Console.Write(" -jar ");
        Console.Write(QuoteForShell(jarLabel));
        foreach (var a in BuildArgumentList(args))
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

    private static (HostingOptions Hosting, SyntheaArgs Args) ParseRunOptions(ParseResult parseResult,
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
        Option<string[]> addFormatOpt,
        Argument<string[]> passthru)
    {
        var javaPathArg = parseResult.GetValue(javaOpt);
        var hosting = new HostingOptions(
            Output: parseResult.GetValue(outputOpt)!,
            Refresh: parseResult.GetValue(refreshOpt),
            JavaPath: string.IsNullOrWhiteSpace(javaPathArg) ? "java" : javaPathArg!);
        var args = new SyntheaArgs(
            State: parseResult.GetValue(stateOpt),
            City: parseResult.GetValue(cityOpt),
            Gender: parseResult.GetValue(genderOpt),
            AgeRange: parseResult.GetValue(ageOpt),
            ModuleDir: parseResult.GetValue(moduleDirOpt),
            Modules: parseResult.GetValue(moduleOpt),
            Population: parseResult.GetValue(popOpt),
            Seed: parseResult.GetValue(seedOpt),
            Config: parseResult.GetValue(configOpt),
            Zip: parseResult.GetValue(zipOpt),
            FhirVersion: parseResult.GetValue(fhirOpt),
            InitialSnapshot: parseResult.GetValue(initSnapOpt),
            UpdatedSnapshot: parseResult.GetValue(updSnapOpt),
            DaysForward: parseResult.GetValue(daysOpt),
            Formats: parseResult.GetValue(formatOpt) ?? Array.Empty<string>(),
            AdditionalFormats: parseResult.GetValue(addFormatOpt) ?? Array.Empty<string>(),
            Passthru: parseResult.GetValue(passthru) ?? Array.Empty<string>());
        return (hosting, args);
    }

    // ----- Option-validator helpers ---------------------------------------
    //
    // Every Create*Option method below shares the same pattern: "if a value
    // was supplied, run a check and add an error when it fails." Two helpers
    // fold that pattern away so each option declaration shows only the rule
    // itself. In System.CommandLine 2.0 GA the validator delegate is
    // Action<OptionResult> — the beta-era named ValidateSymbolResult<T>
    // type is gone (notes §5.4 gotcha).

    private static Action<OptionResult> SingleTokenValidator(Func<string, string?> check) => r =>
    {
        if (r.Tokens.Count == 0) return;
        var err = check(r.Tokens[0].Value);
        if (err is not null) r.AddError(err);
    };

    private static Action<OptionResult> MultiTokenValidator(Func<string, string?> check) => r =>
    {
        foreach (var t in r.Tokens)
        {
            var err = check(t.Value);
            if (err is not null) { r.AddError(err); return; }
        }
    };

    // ----- Per-option factories -------------------------------------------

    private static Option<string?> CreateStateOption()
    {
        // Format-only check (two letters). The previous 50-entry US-state
        // allowlist silently rejected DC, PR, GU, VI, and any future
        // Synthea-supported geo codes. Defer the "is this a real place?"
        // check to Synthea itself, which owns the geo data.
        var opt = new Option<string?>("--state")
        {
            Description = "Two-letter state/territory code (e.g. OH, TX, DC, PR). Format-only check; Synthea rejects unknown codes itself."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
            v.Length == 2 && v.All(char.IsLetter) ? null : "State code must be exactly two letters."));
        return opt;
    }

    private static Option<string?> CreateCityOption()
    {
        var opt = new Option<string?>("--city") { Description = "City name (optional second positional arg after state)" };
        opt.Validators.Add(SingleTokenValidator(v =>
            string.IsNullOrWhiteSpace(v) ? "City name cannot be empty." : null));
        return opt;
    }

    private static Option<string?> CreateGenderOption()
    {
        var opt = new Option<string?>("--gender") { Description = "Patient gender filter (M or F)" };
        opt.Validators.Add(SingleTokenValidator(v =>
        {
            var g = v.ToUpperInvariant();
            return g == "M" || g == "F" ? null : "Gender must be 'M' or 'F'.";
        }));
        return opt;
    }

    private static Option<string?> CreateAgeRangeOption()
    {
        var opt = new Option<string?>("--age-range") { Description = "Age range filter as min-max" };
        opt.Validators.Add(SingleTokenValidator(v =>
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
        var opt = new Option<DirectoryInfo?>("--module-dir") { Description = "Directory of custom modules" };
        opt.Validators.Add(SingleTokenValidator(v =>
            Directory.Exists(v) ? null : "Module directory does not exist."));
        return opt;
    }

    private static Option<string[]> CreateModuleOption()
    {
        var opt = new Option<string[]>("--module")
        {
            Description = "Specific disease modules",
            Arity = ArgumentArity.ZeroOrMore
        };
        opt.Validators.Add(MultiTokenValidator(v =>
            string.IsNullOrWhiteSpace(v) ? "Module name cannot be empty." : null));
        return opt;
    }

    private static Option<int?> CreatePopulationOption()
    {
        var opt = new Option<int?>("--population", "-p") { Description = "Number of patients to generate" };
        opt.Validators.Add(SingleTokenValidator(v =>
            int.TryParse(v, out var n) && n > 0 ? null : "Population must be a positive integer."));
        return opt;
    }

    private static Option<int?> CreateSeedOption()
    {
        var opt = new Option<int?>("--seed", "-s") { Description = "Random seed for deterministic output" };
        opt.Validators.Add(SingleTokenValidator(v =>
            int.TryParse(v, out _) ? null : "Random seed must be an integer."));
        return opt;
    }

    private static Option<FileInfo?> CreateConfigOption()
    {
        var opt = new Option<FileInfo?>("--config", "-c") { Description = "Path to Synthea configuration file" };
        opt.Validators.Add(SingleTokenValidator(v =>
            File.Exists(v) ? null : "Configuration file does not exist."));
        return opt;
    }

    private static Option<string?> CreateZipOption()
    {
        var opt = new Option<string?>("--zip") { Description = "ZIP code (requires --state)" };
        opt.Validators.Add(SingleTokenValidator(v =>
            Regex.IsMatch(v, @"^\d{5}(?:-\d{4})?$") ? null : "ZIP code must be 5 digits or 5+4."));
        return opt;
    }

    private static Option<string?> CreateFhirVersionOption()
    {
        var opt = new Option<string?>("--fhir-version") { Description = "FHIR version (R4 or STU3)" };
        opt.Validators.Add(SingleTokenValidator(v =>
        {
            var u = v.ToUpperInvariant();
            return u == "R4" || u == "STU3" ? null : "FHIR version must be R4 or STU3.";
        }));
        return opt;
    }

    private static Option<FileInfo?> CreateInitialSnapshotOption()
    {
        var opt = new Option<FileInfo?>("--initial-snapshot") { Description = "Path to initial snapshot to load (-i)" };
        opt.Validators.Add(SingleTokenValidator(v =>
            File.Exists(v) ? null : "Initial snapshot file does not exist."));
        return opt;
    }

    private static Option<FileInfo?> CreateUpdatedSnapshotOption()
    {
        var opt = new Option<FileInfo?>("--updated-snapshot") { Description = "Path where updated snapshot will be written (-u)" };
        opt.Validators.Add(SingleTokenValidator(v =>
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
        var opt = new Option<int?>("--days-forward") { Description = "Advance time from snapshot by N days (-t)" };
        opt.Validators.Add(SingleTokenValidator(v =>
            int.TryParse(v, out var n) && n > 0 ? null : "Days forward must be a positive integer."));
        return opt;
    }

    private static Option<string[]> CreateFormatOption()
    {
        var opt = new Option<string[]>("--format")
        {
            Description = "Output formats to generate (FHIR, CSV, CCDA, BULKFHIR, CPCDS). " +
                          "Exclusive: enables only the named formats and disables all others. " +
                          "Use --add-format to extend Synthea defaults instead.",
            Arity = ArgumentArity.ZeroOrMore
        };
        opt.Validators.Add(MultiTokenValidator(v =>
            AllowedFormats.Contains(v) ? null : $"Unsupported format '{v}'."));
        return opt;
    }

    private static Option<string[]> CreateAddFormatOption()
    {
        var opt = new Option<string[]>("--add-format")
        {
            Description = "Additive format to enable in addition to Synthea defaults (FHIR, CSV, CCDA, BULKFHIR, CPCDS). " +
                          "Repeatable. Does not disable any other format; see --format for exclusive semantics.",
            Arity = ArgumentArity.ZeroOrMore
        };
        opt.Validators.Add(MultiTokenValidator(v =>
            AllowedFormats.Contains(v) ? null : $"Unsupported format '{v}'."));
        return opt;
    }

    private static Argument<string[]> CreatePassthruArgument() => new("args")
    {
        Arity = ArgumentArity.ZeroOrMore,
        Description = "Any other arguments forwarded unchanged to synthea.jar"
    };
}
