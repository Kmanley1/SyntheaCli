using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Synthea.Cli;

internal static class RunCommand
{
    private static readonly HashSet<string> ValidStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA","HI","ID","IL","IN","IA","KS",
        "KY","LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV","NH","NJ","NM","NY",
        "NC","ND","OH","OK","OR","PA","RI","SC","SD","TN","TX","UT","VT","VA","WA","WV","WI","WY"
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
        runCmd.AddArgument(passthru);

        runCmd.TreatUnmatchedTokensAsErrors = false;

        runCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var opts = ParseRunOptions(ctx, refreshOpt, javaOpt, outputOpt, stateOpt, cityOpt,
                genderOpt, ageOpt, moduleDirOpt, moduleOpt, popOpt, seedOpt, configOpt, zipOpt,
                fhirVerOpt, initialSnapOpt, updatedSnapOpt, daysForwardOpt, formatOpt, passthru);

            Directory.CreateDirectory(opts.Output.FullName);

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

            var jar = await Program.EnsureJarAsyncFunc(
                          opts.Refresh,
                          new Progress<(long dl, long total)>(p =>
                              Console.Write($"\rDownloading Synthea {p.dl / 1_000_000}/{p.total / 1_000_000} MB…")),
                          ctx.GetCancellationToken());

            Console.WriteLine($"\n✓ Using {jar.Name}");

            var psi = CreateProcessStartInfo(opts, jar);

            using var proc = Program.Runner.Start(psi);
            var pumpOut = Task.Run(() => ProcessHelpers.Relay(proc.StandardOutput, Console.Out));
            var pumpErr = Task.Run(() => ProcessHelpers.Relay(proc.StandardError, Console.Error));

            await Task.WhenAll(pumpOut, pumpErr, proc.WaitForExitAsync());
            ctx.ExitCode = proc.ExitCode;
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

    private static Option<string?> CreateStateOption()
    {
        var stateOpt = new Option<string?>("--state", "Two-letter state code (e.g. OH, TX). Adds it as positional state arg");
        stateOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var v = r.Tokens[0].Value.ToUpperInvariant();
            if (v.Length != 2 || !ValidStates.Contains(v))
                r.ErrorMessage = "State must be a valid two letter code.";
        });
        return stateOpt;
    }

    private static Option<string?> CreateCityOption()
    {
        var cityOpt = new Option<string?>("--city", "City name (optional second positional arg after state)");
        cityOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (string.IsNullOrWhiteSpace(r.Tokens[0].Value))
                r.ErrorMessage = "City name cannot be empty.";
        });
        return cityOpt;
    }

    private static Option<string?> CreateGenderOption()
    {
        var genderOpt = new Option<string?>("--gender", "Patient gender filter (M or F)");
        genderOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var g = r.Tokens[0].Value.ToUpperInvariant();
            if (g != "M" && g != "F")
                r.ErrorMessage = "Gender must be 'M' or 'F'.";
        });
        return genderOpt;
    }

    private static Option<string?> CreateAgeRangeOption()
    {
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
        return ageOpt;
    }

    private static Option<DirectoryInfo?> CreateModuleDirOption()
    {
        var moduleDirOpt = new Option<DirectoryInfo?>("--module-dir", "Directory of custom modules");
        moduleDirOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!Directory.Exists(r.Tokens[0].Value))
                r.ErrorMessage = "Module directory does not exist.";
        });
        return moduleDirOpt;
    }

    private static Option<string[]> CreateModuleOption()
    {
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
        return moduleOpt;
    }

    private static Option<int?> CreatePopulationOption()
    {
        var popOpt = new Option<int?>(aliases: new[] { "--population", "-p" }, description: "Number of patients to generate");
        popOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!int.TryParse(r.Tokens[0].Value, out var v) || v <= 0)
                r.ErrorMessage = "Population must be a positive integer.";
        });
        return popOpt;
    }

    private static Option<int?> CreateSeedOption()
    {
        var seedOpt = new Option<int?>(aliases: new[] { "--seed", "-s" }, description: "Random seed for deterministic output");
        seedOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!int.TryParse(r.Tokens[0].Value, out _))
                r.ErrorMessage = "Random seed must be an integer.";
        });
        return seedOpt;
    }

    private static Option<FileInfo?> CreateConfigOption()
    {
        var configOpt = new Option<FileInfo?>(aliases: new[] { "--config", "-c" }, description: "Path to Synthea configuration file");
        configOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!File.Exists(r.Tokens[0].Value))
                r.ErrorMessage = "Configuration file does not exist.";
        });
        return configOpt;
    }

    private static Option<string?> CreateZipOption()
    {
        var zipOpt = new Option<string?>("--zip", "ZIP code (requires --state)");
        zipOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!Regex.IsMatch(r.Tokens[0].Value, @"^\d{5}(?:-\d{4})?$"))
                r.ErrorMessage = "ZIP code must be 5 digits or 5+4.";
        });
        return zipOpt;
    }

    private static Option<string?> CreateFhirVersionOption()
    {
        var fhirVerOpt = new Option<string?>("--fhir-version", "FHIR version (R4 or STU3)");
        fhirVerOpt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var v = r.Tokens[0].Value.ToUpperInvariant();
            if (v != "R4" && v != "STU3")
                r.ErrorMessage = "FHIR version must be R4 or STU3.";
        });
        return fhirVerOpt;
    }

    private static Option<FileInfo?> CreateInitialSnapshotOption()
    {
        var opt = new Option<FileInfo?>("--initial-snapshot", "Path to initial snapshot to load (-i)");
        opt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!File.Exists(r.Tokens[0].Value))
                r.ErrorMessage = "Initial snapshot file does not exist.";
        });
        return opt;
    }

    private static Option<FileInfo?> CreateUpdatedSnapshotOption()
    {
        var opt = new Option<FileInfo?>("--updated-snapshot", "Path where updated snapshot will be written (-u)");
        opt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            var dir = Path.GetDirectoryName(r.Tokens[0].Value);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                r.ErrorMessage = "Directory for updated snapshot must exist.";
        });
        return opt;
    }

    private static Option<int?> CreateDaysForwardOption()
    {
        var opt = new Option<int?>("--days-forward", "Advance time from snapshot by N days (-t)");
        opt.AddValidator(r =>
        {
            if (r.Tokens.Count == 0) return;
            if (!int.TryParse(r.Tokens[0].Value, out var v) || v <= 0)
                r.ErrorMessage = "Days forward must be a positive integer.";
        });
        return opt;
    }

    private static Option<string[]> CreateFormatOption()
    {
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
        return formatOpt;
    }

    private static Argument<string[]> CreatePassthruArgument()
    {
        return new Argument<string[]>("args")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "Any other arguments forwarded unchanged to synthea.jar"
        };
    }
}
