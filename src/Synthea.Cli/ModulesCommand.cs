using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

namespace Synthea.Cli;

// `synthea modules list [--module-dir <dir>]` — enumerate JAR + filesystem
// modules. `synthea modules describe <name>` — pretty-print one module's
// remarks, GMF version, and state count. (B4)
internal static class ModulesCommand
{
    internal static Command Build(IJarSource jarSource)
    {
        var cmd = new Command("modules", "Inspect Synthea modules bundled in the cached JAR or a --module-dir");
        cmd.Subcommands.Add(BuildList(jarSource));
        cmd.Subcommands.Add(BuildDescribe(jarSource));
        return cmd;
    }

    private static Command BuildList(IJarSource jarSource)
    {
        var list = new Command("list", "List all modules in the cached Synthea JAR and an optional --module-dir");
        var moduleDirOpt = new Option<DirectoryInfo?>("--module-dir")
        {
            Description = "Additional directory of .json modules to enumerate alongside the JAR contents."
        };
        list.Options.Add(moduleDirOpt);

        list.SetAction(parseResult =>
        {
            var jar = jarSource.TryFindCachedJar();
            var moduleDir = parseResult.GetValue(moduleDirOpt);

            if (jar is null && moduleDir is null)
            {
                Console.Error.WriteLine("No cached JAR found and no --module-dir given. Run `synthea run --print-args` once to fetch the JAR, or pass --module-dir.");
                return 1;
            }

            var allEntries = new List<ModuleEntry>();
            if (jar is not null)
            {
                try
                {
                    allEntries.AddRange(ModuleIntrospector.ListJarModules(jar.FullName, ResolveCacheDir()));
                }
                catch (InvalidDataException ex)
                {
                    Console.Error.WriteLine($"error: cached JAR is not a valid zip: {ex.Message}");
                    return 1;
                }
            }
            if (moduleDir is not null)
            {
                try
                {
                    allEntries.AddRange(ModuleIntrospector.ListDirectoryModules(moduleDir.FullName));
                }
                catch (DirectoryNotFoundException ex)
                {
                    Console.Error.WriteLine($"error: {ex.Message}");
                    return 1;
                }
            }

            if (allEntries.Count == 0)
            {
                Console.WriteLine("No modules found.");
                return 0;
            }

            Console.WriteLine($"{allEntries.Count} module(s):");
            var nameWidth = Math.Min(48, allEntries.Max(e => e.Name.Length));
            foreach (var e in allEntries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                var origin = e.Source == ModuleSource.Jar ? "[JAR]" : "[DIR]";
                Console.WriteLine($"  {origin}  {e.Name.PadRight(nameWidth)}  {e.Location}");
            }
            return 0;
        });

        return list;
    }

    private static Command BuildDescribe(IJarSource jarSource)
    {
        var describe = new Command("describe", "Show a module's remarks, GMF version, and state count");
        var nameArg = new Argument<string>("name")
        {
            Description = "Module name (e.g. 'asthma') or full JAR entry path (e.g. 'modules/asthma.json')."
        };
        var moduleDirOpt = new Option<DirectoryInfo?>("--module-dir")
        {
            Description = "Look in this directory first; falls back to the cached JAR if not found."
        };
        describe.Arguments.Add(nameArg);
        describe.Options.Add(moduleDirOpt);

        describe.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(nameArg)!;
            var moduleDir = parseResult.GetValue(moduleDirOpt);

            // Prefer a filesystem module if --module-dir was passed AND
            // contains a matching file; otherwise fall through to the JAR.
            if (moduleDir is not null)
            {
                var direct = Path.Combine(moduleDir.FullName, name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name : name + ".json");
                if (File.Exists(direct))
                {
                    return PrintDescription(ModuleIntrospector.DescribeFileModule(direct));
                }
            }

            var jar = jarSource.TryFindCachedJar();
            if (jar is null)
            {
                Console.Error.WriteLine("No cached JAR found. Run `synthea run --print-args` once to fetch the JAR, or pass --module-dir <dir> to describe a filesystem module.");
                return 1;
            }
            try
            {
                return PrintDescription(ModuleIntrospector.DescribeJarModule(jar.FullName, name));
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                return 1;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"error: cached JAR is not a valid zip: {ex.Message}");
                return 1;
            }
        });

        return describe;
    }

    internal static int PrintDescription(ModuleDescription d)
    {
        Console.WriteLine($"Name:        {d.Name}");
        Console.WriteLine($"Location:    {d.Location}");
        Console.WriteLine($"GMF version: {d.GmfVersion ?? "(unspecified)"}");
        Console.WriteLine($"States:      {d.StateCount}");
        if (!string.IsNullOrWhiteSpace(d.Remarks))
        {
            Console.WriteLine();
            Console.WriteLine("Remarks:");
            foreach (var line in d.Remarks.Split('\n'))
                Console.WriteLine($"  {line.TrimEnd()}");
        }
        return 0;
    }

    // Cache file lives next to config.json in ~/.synthea-cli/ rather than
    // in the JAR cache dir, because the JAR cache is intended to be safe to
    // delete via `synthea cache clear`, but the module list cache is keyed
    // by JAR SHA so the entry is naturally invalidated on JAR refresh.
    internal static string ResolveCacheDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".synthea-cli");
}
