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

    internal static Command Build(IProcessRunner runner, IJarSource jarSource, IJavaDetector javaDetector,
        Option<bool> refreshOpt, Option<string?> javaOpt, Option<bool> skipJdkCheckOpt)
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
        var jarOpt = new Option<string?>("--jar")
        {
            Description = "Path to a Synthea JAR to use directly. Skips the GitHub download. " +
                          "Falls back to SYNTHEA_CLI_JAR_PATH env var, then ~/.synthea-cli/config.json."
        };
        var insistChecksumOpt = new Option<bool>("--insist-checksum")
        {
            Description = "Fail the run if the upstream release does not publish a .sha256 asset."
        };
        var referenceDateOpt = CreateReferenceDateOption();   // A1
        var endDateOpt = CreateEndDateOption();               // A1
        var allowFutureEndOpt = new Option<bool>("--allow-future-end")
        {
            Description = "Permit --end-date beyond today (Synthea -E)."
        };
        var clinicianSeedOpt = CreateClinicianSeedOption();   // A1
        var singlePersonSeedOpt = CreateSinglePersonSeedOption(); // A1
        var overflowOpt = new Option<bool>("--overflow")
        {
            Description = "Allow Synthea's overflow generation (-o true). " +
                          "Off by default; turn on for full-fidelity reruns of past results."
        };
        var propertyOpt = CreatePropertyOption();                 // A6
        var usCoreVerOpt = CreateUsCoreVersionOption();           // A9
        var flexporterOpt = CreateFlexporterMappingOption();      // A5
        var igDirOpt = CreateIgDirOption();                       // A5
        var bulkDataOpt = new Option<bool>("--bulk-data")         // A8
        {
            Description = "Emit FHIR resources as bulk-data NDJSON (--exporter.fhir.bulk_data=true)."
        };
        var javaHeapOpt = CreateJavaHeapOption();                 // C3
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
        runCmd.Options.Add(jarOpt);
        runCmd.Options.Add(insistChecksumOpt);
        runCmd.Options.Add(referenceDateOpt);
        runCmd.Options.Add(endDateOpt);
        runCmd.Options.Add(allowFutureEndOpt);
        runCmd.Options.Add(clinicianSeedOpt);
        runCmd.Options.Add(singlePersonSeedOpt);
        runCmd.Options.Add(overflowOpt);
        runCmd.Options.Add(propertyOpt);
        runCmd.Options.Add(usCoreVerOpt);
        runCmd.Options.Add(flexporterOpt);
        runCmd.Options.Add(igDirOpt);
        runCmd.Options.Add(bulkDataOpt);
        runCmd.Options.Add(javaHeapOpt);
        runCmd.Arguments.Add(passthru);

        runCmd.TreatUnmatchedTokensAsErrors = false;

        runCmd.SetAction(async (ParseResult parseResult, CancellationToken cancelToken) =>
        {
            var (hosting, args) = ParseRunOptions(parseResult, refreshOpt, javaOpt, outputOpt, stateOpt, cityOpt,
                genderOpt, ageOpt, moduleDirOpt, moduleOpt, popOpt, seedOpt, configOpt, zipOpt,
                fhirVerOpt, initialSnapOpt, updatedSnapOpt, daysForwardOpt, formatOpt, addFormatOpt,
                jarOpt, insistChecksumOpt, passthru,
                referenceDateOpt, endDateOpt, allowFutureEndOpt, clinicianSeedOpt, singlePersonSeedOpt, overflowOpt,
                propertyOpt, usCoreVerOpt, flexporterOpt, igDirOpt, bulkDataOpt);
            // C3: heap override lives outside the hosting/args records — it
            // affects only the JVM line we build below, not anything Synthea
            // sees or anything the test fixtures need to know about.
            var heapOverride = parseResult.GetValue(javaHeapOpt);
            var printArgs = parseResult.GetValue(printArgsOpt);

            // C7: malformed ~/.synthea-cli/config.json must fail the run with
            // a clean exit 1 (not a stack trace, not exit 3 from the JAR
            // catch). Load explicitly here so we can map the error cleanly.
            CliConfig config;
            try
            {
                config = CliConfig.LoadOrThrow();
            }
            catch (CliConfigException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
            var jarOverrides = ResolveJarOverrides(hosting, config, Environment.GetEnvironmentVariable);

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
                return PrintInvocation(hosting, args, jarSource, heapOverride);
            }

            var skipJdk = parseResult.GetValue(skipJdkCheckOpt);
            if (!skipJdk)
            {
                var probe = await javaDetector.ProbeAsync(hosting.JavaPath, cancelToken);
                if (!probe.Found)
                {
                    Console.Error.WriteLine($"Java not found at '{hosting.JavaPath}'. Use --java-path or install OpenJDK 17 LTS.");
                    if (!string.IsNullOrWhiteSpace(probe.ErrorMessage))
                        Console.Error.WriteLine($"Details: {probe.ErrorMessage}");
                    return 3;
                }
                if (probe.MajorVersion is { } major && major < 17)
                {
                    Console.Error.WriteLine(
                        $"Synthea requires Java 17 or later (found Java {major}). " +
                        "Install OpenJDK 17 LTS or newer; see https://adoptium.net.");
                    return 1;
                }
            }
            else
            {
                Console.Error.WriteLine("warning: --skip-jdk-check set; bypassing Java 17+ preflight.");
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

                var jar = await jarSource.EnsureJarAsync(hosting.Refresh, progress, cancelToken, jarOverrides);

                if (interactive) Console.WriteLine();
                Console.WriteLine($"✓ Using {jar.Name}  ({jar.FullName})");

                var psi = CreateProcessStartInfo(hosting, args, jar, heapOverride);

                using var proc = runner.Start(psi);
                using var killReg = cancelToken.Register(() =>
                {
                    try { proc.Kill(entireProcessTree: true); } catch { /* already exited */ }
                });
                var pumpOut = Task.Run(() => ProcessHelpers.Relay(proc.StandardOutput, Console.Out));
                var pumpErr = Task.Run(() => ProcessHelpers.RelayAndCapture(proc.StandardError, Console.Error));

                await Task.WhenAll(pumpOut, pumpErr, proc.WaitForExitAsync());
                var exitCode = proc.ExitCode;
                if (exitCode != 0)
                {
                    var capturedStderr = string.Join('\n', pumpErr.Result);
                    var hint = SyntheaErrorPatterns.TryGetRemediation(capturedStderr);
                    if (hint is not null)
                    {
                        Console.Error.WriteLine();
                        Console.Error.WriteLine($"hint: {hint}");
                    }
                }
                return exitCode;
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
            // C4: callers cross-OS habituated to one path separator can pass
            // the other and we silently translate. Synthea's `-m` is
            // colon/semicolon joined via java.io.File.pathSeparator (`:`
            // on Unix, `;` on Windows); a Linux scripter who pipes the
            // same command to a Windows runner shouldn't break.
            foreach (var m in o.Modules)
            {
                argList.Add("--module");
                argList.Add(NormalizeModuleSeparators(m));
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
        // A1: reproducibility flags. Convert ISO YYYY-MM-DD → YYYYMMDD,
        // which is the form Synthea's date parser expects.
        if (!string.IsNullOrWhiteSpace(o.ReferenceDate))
        {
            argList.Add("-r");
            argList.Add(o.ReferenceDate!.Replace("-", string.Empty));
        }
        if (!string.IsNullOrWhiteSpace(o.EndDate))
        {
            argList.Add("-e");
            argList.Add(o.EndDate!.Replace("-", string.Empty));
        }
        if (o.AllowFutureEnd)
        {
            argList.Add("-E");
        }
        if (o.ClinicianSeed.HasValue)
        {
            argList.Add("-cs");
            argList.Add(o.ClinicianSeed.Value.ToString());
        }
        if (o.SinglePersonSeed.HasValue)
        {
            argList.Add("-ps");
            argList.Add(o.SinglePersonSeed.Value.ToString());
        }
        // A2/A3: overflow. Synthea -o true vs false; off by default.
        if (o.Overflow)
        {
            argList.Add("-o");
            argList.Add("true");
        }
        // A5: Flexporter mapping (-fm <path>) and IG dir (-ig <dir>).
        // Both Synthea flag-form; emit before --property/format blocks and
        // before positional state so Synthea parses them as flags, not
        // positionals. Paths normalized to absolute so cwd changes don't
        // break the JAR's view of them.
        if (o.FlexporterMapping is not null)
        {
            argList.Add("-fm");
            argList.Add(Path.GetFullPath(o.FlexporterMapping.FullName));
        }
        if (o.IgDir is not null)
        {
            argList.Add("-ig");
            argList.Add(Path.GetFullPath(o.IgDir.FullName));
        }
        // A8: bulk-data property — companion to the format block below;
        // emitted as Synthea's `--exporter.fhir.bulk_data=true`.
        if (o.BulkData)
        {
            argList.Add("--exporter.fhir.bulk_data=true");
        }
        // A6: each --property KEY=VALUE becomes Synthea's documented
        // `--KEY=VALUE` form. The validator on the option guarantees a
        // single '=' and a well-formed key, so we don't re-check here.
        if (o.Properties is not null)
        {
            foreach (var kv in o.Properties)
            {
                argList.Add("--" + kv);
            }
        }
        // C5: With more than 10k patients in one directory, filesystem
        // browsers and shells (mostly Windows Explorer) get slow. Enabling
        // Synthea's id-substring subfolder bucketing keeps the JSON output
        // tree usable. We skip this if the caller already set the property
        // via --property so an explicit override always wins.
        if (o.Population is int pop && pop > SubfoldersAutoThreshold
            && !PropertiesContainKey(o.Properties, SubfoldersPropertyKey))
        {
            argList.Add($"--{SubfoldersPropertyKey}=true");
        }
        if (!string.IsNullOrWhiteSpace(o.FhirVersion))
        {
            argList.Add("--exporter.fhir.version=" + o.FhirVersion!.ToUpperInvariant());
        }
        // (A9) US Core IG enable + version. Must precede the format-export
        // block so Synthea reads the IG choice before deciding what to
        // write. Validator on --us-core-version restricts to the published
        // set, so we don't re-check the value here.
        if (!string.IsNullOrWhiteSpace(o.UsCoreVersion))
        {
            argList.Add("--exporter.fhir.use_us_core_ig=true");
            argList.Add($"--exporter.fhir.us_core_version={o.UsCoreVersion}");
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

    internal static ProcessStartInfo CreateProcessStartInfo(
        HostingOptions hosting, SyntheaArgs args, FileInfo jar, string? heapOverride = null)
    {
        var psi = new ProcessStartInfo(hosting.JavaPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = hosting.Output.FullName
        };
        // C3: JVM args must precede `-jar`. If the user supplied --java-heap
        // we honor it; otherwise we let JavaHeapSizer pick a tier based on
        // --population. Sub-1000 populations get no heap arg so the JVM
        // default applies.
        var heap = JavaHeapSizer.Resolve(heapOverride, args.Population);
        if (!string.IsNullOrWhiteSpace(heap))
            psi.ArgumentList.Add(heap);
        psi.ArgumentList.Add("-jar");
        psi.ArgumentList.Add(jar.FullName);
        foreach (var a in BuildArgumentList(args))
            psi.ArgumentList.Add(a);
        return psi;
    }

    private static int PrintInvocation(HostingOptions hosting, SyntheaArgs args, IJarSource jarSource, string? heapOverride)
    {
        var cachedJar = jarSource.TryFindCachedJar();
        var jarLabel = cachedJar?.FullName
            ?? "<synthea.jar — not yet cached; run once without --print-args>";
        Console.WriteLine($"# Java executable: {hosting.JavaPath}");
        Console.WriteLine($"# Synthea JAR:     {jarLabel}");
        Console.WriteLine($"# Working dir:     {hosting.Output.FullName}");
        Console.Write(QuoteForShell(hosting.JavaPath));
        var heap = JavaHeapSizer.Resolve(heapOverride, args.Population);
        if (!string.IsNullOrWhiteSpace(heap))
        {
            Console.Write(' ');
            Console.Write(QuoteForShell(heap));
        }
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
        Option<string?> jarOpt,
        Option<bool> insistChecksumOpt,
        Argument<string[]> passthru,
        Option<string?> referenceDateOpt,
        Option<string?> endDateOpt,
        Option<bool> allowFutureEndOpt,
        Option<int?> clinicianSeedOpt,
        Option<int?> singlePersonSeedOpt,
        Option<bool> overflowOpt,
        Option<string[]> propertyOpt,
        Option<string?> usCoreVerOpt,
        Option<FileInfo?> flexporterOpt,
        Option<DirectoryInfo?> igDirOpt,
        Option<bool> bulkDataOpt)
    {
        var javaPathArg = parseResult.GetValue(javaOpt);
        var hosting = new HostingOptions(
            Output: parseResult.GetValue(outputOpt)!,
            Refresh: parseResult.GetValue(refreshOpt),
            JavaPath: string.IsNullOrWhiteSpace(javaPathArg) ? "java" : javaPathArg!,
            JarPath: parseResult.GetValue(jarOpt),
            InsistChecksum: parseResult.GetValue(insistChecksumOpt));
        var args = new SyntheaArgs(
            State: UsStates.Normalize(parseResult.GetValue(stateOpt)),
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
            Passthru: parseResult.GetValue(passthru) ?? Array.Empty<string>(),
            ReferenceDate: parseResult.GetValue(referenceDateOpt),
            EndDate: parseResult.GetValue(endDateOpt),
            AllowFutureEnd: parseResult.GetValue(allowFutureEndOpt),
            ClinicianSeed: parseResult.GetValue(clinicianSeedOpt),
            SinglePersonSeed: parseResult.GetValue(singlePersonSeedOpt),
            Overflow: parseResult.GetValue(overflowOpt),
            Properties: parseResult.GetValue(propertyOpt),
            UsCoreVersion: parseResult.GetValue(usCoreVerOpt),
            FlexporterMapping: parseResult.GetValue(flexporterOpt),
            IgDir: parseResult.GetValue(igDirOpt),
            BulkData: parseResult.GetValue(bulkDataOpt));
        return (hosting, args);
    }

    // ----- Configuration resolution ---------------------------------------
    //
    // Apply the four-source precedence rule for the JarManager inputs:
    //   CLI flag > env var > ~/.synthea-cli/config.json > built-in default
    // CLI values arrive on HostingOptions; env vars and config file are
    // resolved here so JarManager doesn't have to know about either. (A-40)

    internal static JarOverrides ResolveJarOverrides(HostingOptions hosting)
        => ResolveJarOverrides(hosting, CliConfig.Load(), Environment.GetEnvironmentVariable);

    internal static JarOverrides ResolveJarOverrides(HostingOptions hosting, CliConfig config, Func<string, string?> envGetter)
    {
        var jarPath = CliConfig.Resolve(hosting.JarPath, "SYNTHEA_CLI_JAR_PATH", config.JarPath, envGetter);
        var token = CliConfig.Resolve(null, "GITHUB_TOKEN", config.GitHubToken, envGetter);
        var insist = CliConfig.ResolveBool(hosting.InsistChecksum, "SYNTHEA_CLI_INSIST_CHECKSUM", config.InsistChecksum, envGetter);
        return new JarOverrides(jarPath, token, insist);
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
        // Accept either a 2-letter USPS code (e.g. "OH") or a full state
        // name (e.g. "Ohio", "New Hampshire"). 2-letter codes are converted
        // to the full name in ParseRunOptions before passthru, because
        // Synthea's geography data is keyed by full name and rejects bare
        // 2-letter codes. (C1) The validator now also rejects full names
        // outside the known 56-state set, with a "did you mean ...?" hint
        // for likely misspellings — failing fast in the CLI is cheaper than
        // letting Synthea blow up with an opaque stack trace minutes later.
        var opt = new Option<string?>("--state")
        {
            Description = "State name (e.g. 'Ohio') or two-letter USPS code (e.g. 'OH'). 2-letter codes are converted to the full name automatically. Rejects unknown names with a suggestion."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
        {
            if (v.Length == 2 && v.All(char.IsLetter))
            {
                return UsStates.IsKnownCode(v)
                    ? null
                    : $"Unknown 2-letter state code '{v.ToUpperInvariant()}'. Use a USPS code (e.g. OH, TX, DC) or the full name (e.g. Ohio).";
            }
            if (v.Length < 3) return "State must be a 2-letter USPS code or the full state name.";
            if (UsStates.IsKnownFullName(v)) return null;
            var suggestion = UsStates.SuggestClosest(v);
            return suggestion is not null
                ? $"Unknown state '{v}'. Did you mean '{suggestion}'?"
                : $"Unknown state '{v}'. Use a 2-letter USPS code (e.g. OH) or full state name (e.g. Ohio).";
        }));
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
        // (A10) R5 added to the allowlist. Synthea ≥ v4 supports R5
        // exporters; older versions silently no-op. The CLI is permissive
        // either way — Synthea is the source of truth for export support.
        var opt = new Option<string?>("--fhir-version") { Description = "FHIR version (STU3, R4, or R5)" };
        opt.Validators.Add(SingleTokenValidator(v =>
        {
            var u = v.ToUpperInvariant();
            return u == "R4" || u == "STU3" || u == "R5" ? null : "FHIR version must be STU3, R4, or R5.";
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

    // A1: reproducibility window — accepts ISO YYYY-MM-DD; emitted to
    // Synthea as YYYYMMDD (its accepted form) by BuildArgumentList.
    private static Option<string?> CreateReferenceDateOption()
    {
        var opt = new Option<string?>("--reference-date")
        {
            Description = "Reference date (ISO YYYY-MM-DD). Maps to Synthea -r YYYYMMDD."
        };
        opt.Validators.Add(SingleTokenValidator(ValidateIsoDate));
        return opt;
    }

    private static Option<string?> CreateEndDateOption()
    {
        var opt = new Option<string?>("--end-date")
        {
            Description = "Simulation end date (ISO YYYY-MM-DD). Maps to Synthea -e YYYYMMDD. " +
                          "Requires --allow-future-end if the date is beyond today."
        };
        opt.Validators.Add(SingleTokenValidator(ValidateIsoDate));
        return opt;
    }

    private static Option<int?> CreateClinicianSeedOption()
    {
        var opt = new Option<int?>("--clinician-seed")
        {
            Description = "Random seed for clinician generation (Synthea -cs N)."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
            int.TryParse(v, out _) ? null : "Clinician seed must be an integer."));
        return opt;
    }

    private static Option<int?> CreateSinglePersonSeedOption()
    {
        var opt = new Option<int?>("--single-person-seed")
        {
            Description = "Random seed for single-person generation (Synthea -ps N)."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
            int.TryParse(v, out _) ? null : "Single-person seed must be an integer."));
        return opt;
    }

    // A6: --property KEY=VALUE, repeatable. Lets callers pass any Synthea
    // property we haven't surfaced as a first-class flag without dropping
    // into the bare passthru argument tail.
    private static readonly Regex PropertyKeyRegex =
        new("^[a-zA-Z][a-zA-Z0-9._-]*$", RegexOptions.Compiled);

    private static Option<string[]> CreatePropertyOption()
    {
        var opt = new Option<string[]>("--property")
        {
            Description = "Set a Synthea property as KEY=VALUE (repeatable). " +
                          "Emitted as --KEY=VALUE. Use for properties without a first-class flag.",
            Arity = ArgumentArity.ZeroOrMore
        };
        opt.Validators.Add(MultiTokenValidator(ValidateProperty));
        return opt;
    }

    // A5: Flexporter mapping file (`-fm`). Existence is checked at parse
    // time so we fail before launching the JVM rather than after.
    private static Option<FileInfo?> CreateFlexporterMappingOption()
    {
        var opt = new Option<FileInfo?>("--flexporter-mapping")
        {
            Description = "Path to a Flexporter mapping file (.yaml/.json). Maps to Synthea -fm <path>."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
            File.Exists(v) ? null : $"Flexporter mapping file does not exist: '{v}'."));
        return opt;
    }

    // A5: Custom Implementation Guide directory (`-ig`). Same pattern as
    // --module-dir; existence checked up-front.
    private static Option<DirectoryInfo?> CreateIgDirOption()
    {
        var opt = new Option<DirectoryInfo?>("--ig-dir")
        {
            Description = "Directory of FHIR Implementation Guide resources. Maps to Synthea -ig <dir>."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
            Directory.Exists(v) ? null : $"IG directory does not exist: '{v}'."));
        return opt;
    }

    // A9: US Core IG version allowlist. The full set Synthea exposes via
    // its `exporter.fhir.us_core_version` property as of v3.3+. New
    // versions ship rarely; expanding the allowlist is a deliberate act.
    internal static readonly string[] UsCoreVersions = { "3.1.1", "4", "5", "6", "7" };

    private static Option<string?> CreateUsCoreVersionOption()
    {
        var opt = new Option<string?>("--us-core-version")
        {
            Description = "US Core IG version to apply to FHIR export. " +
                          "Allowed: " + string.Join(", ", UsCoreVersions) + ". " +
                          "Implies --exporter.fhir.use_us_core_ig=true."
        };
        opt.Validators.Add(SingleTokenValidator(v =>
            Array.IndexOf(UsCoreVersions, v) >= 0
                ? null
                : $"US Core version must be one of: {string.Join(", ", UsCoreVersions)}."));
        return opt;
    }

    internal static string? ValidateProperty(string v)
    {
        if (string.IsNullOrEmpty(v))
            return "Property must be KEY=VALUE.";
        var eq = v.IndexOf('=');
        if (eq < 0)
            return $"Property '{v}' must be KEY=VALUE.";
        if (v.IndexOf('=', eq + 1) >= 0)
            return $"Property '{v}' must contain exactly one '=' (KEY=VALUE).";
        var key = v.Substring(0, eq);
        if (!PropertyKeyRegex.IsMatch(key))
            return $"Property key '{key}' must match [a-zA-Z][a-zA-Z0-9._-]*.";
        return null;
    }

    // ISO-8601 calendar date (YYYY-MM-DD), strict. Bare-yyyy / yyyy-mm
    // shortcuts not accepted — Synthea wants full date precision and we
    // don't want to silently coerce.
    private static string? ValidateIsoDate(string v)
        => DateTime.TryParseExact(v, "yyyy-MM-dd",
                                  System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None, out _)
            ? null
            : $"Date must be ISO YYYY-MM-DD (got '{v}').";

    // ----- Platform niceties (C4 + C5) -----------------------------------

    internal const string SubfoldersPropertyKey = "exporter.subfolders_by_id_substring";
    internal const int SubfoldersAutoThreshold = 10_000;

    internal static string NormalizeModuleSeparators(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var foreign = OperatingSystem.IsWindows() ? ':' : ';';
        var local = Path.PathSeparator;
        return foreign == local ? value : value.Replace(foreign, local);
    }

    internal static bool PropertiesContainKey(string[]? properties, string key)
    {
        if (properties is null || properties.Length == 0) return false;
        var prefix = key + "=";
        foreach (var p in properties)
        {
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // C3: --java-heap <size> — overrides the population-based heap tier.
    // The value is the raw -Xmx payload (e.g. "4g", "1024m"); we accept
    // shapes that match `\d+[gGmM]` and prefix `-Xmx` in JavaHeapSizer
    // before handing off to the JVM.
    private static Option<string?> CreateJavaHeapOption()
    {
        var opt = new Option<string?>("--java-heap")
        {
            Description = "Override the auto-sized JVM heap (e.g. '4g' or '1024m'). " +
                          "Defaults: 2g for >=1k, 4g for >=10k, 8g for >=100k, 16g for >=1M; " +
                          "no flag for <1k (use JVM default)."
        };
        opt.Validators.Add(SingleTokenValidator(JavaHeapSizer.ValidateOverride));
        return opt;
    }
}
