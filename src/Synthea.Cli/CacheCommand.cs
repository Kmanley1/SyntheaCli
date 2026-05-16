using System;
using System.CommandLine;
using System.IO;
using System.Linq;

namespace Synthea.Cli;

// `synthea cache list` / `synthea cache clear` (A-14). The cache root is
// %LOCALAPPDATA%\Synthea.Cli on Windows and ~/.local/share/Synthea.Cli on
// Linux/macOS — resolved by IJarSource.CachePath so this command does not
// duplicate JarManager's path logic.
internal static class CacheCommand
{
    internal static Command Build(IJarSource jarSource)
    {
        var cmd = new Command("cache", "Manage the local Synthea JAR cache");
        cmd.Subcommands.Add(BuildList(jarSource));
        cmd.Subcommands.Add(BuildClear(jarSource));
        return cmd;
    }

    private static Command BuildList(IJarSource jarSource)
    {
        var list = new Command("list", "List cached Synthea JARs with size and last-modified date");
        list.SetAction(_ =>
        {
            var dir = jarSource.CachePath;
            Console.WriteLine($"Cache: {dir}");
            if (!Directory.Exists(dir))
            {
                Console.WriteLine("  (directory does not exist yet)");
                return 0;
            }
            var files = Directory.GetFiles(dir);
            if (files.Length == 0)
            {
                Console.WriteLine("  (empty)");
                return 0;
            }
            foreach (var path in files.OrderBy(f => File.GetLastWriteTimeUtc(f)))
            {
                var fi = new FileInfo(path);
                Console.WriteLine($"  {fi.LastWriteTimeUtc:yyyy-MM-dd HH:mm}  {fi.Length,12:n0}  {fi.Name}");
            }
            return 0;
        });
        return list;
    }

    private static Command BuildClear(IJarSource jarSource)
    {
        var clear = new Command("clear", "Delete all cached Synthea JARs");
        var yesOpt = new Option<bool>("--yes", "-y")
        {
            Description = "Skip the interactive confirmation prompt"
        };
        clear.Options.Add(yesOpt);

        clear.SetAction(parseResult =>
        {
            var dir = jarSource.CachePath;
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"Cache directory does not exist: {dir}");
                return 0;
            }
            var files = Directory.GetFiles(dir);
            if (files.Length == 0)
            {
                Console.WriteLine($"Cache is already empty: {dir}");
                return 0;
            }
            var skipPrompt = parseResult.GetValue(yesOpt);
            if (!skipPrompt)
            {
                Console.Write($"Delete {files.Length} file(s) from {dir}? [y/N] ");
                var resp = Console.ReadLine();
                if (string.IsNullOrEmpty(resp) || !resp.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Aborted.");
                    return 1;
                }
            }
            var deleted = 0;
            foreach (var path in files)
            {
                try { File.Delete(path); deleted++; }
                catch (IOException ex) { Console.Error.WriteLine($"Skipped {Path.GetFileName(path)}: {ex.Message}"); }
                catch (UnauthorizedAccessException ex) { Console.Error.WriteLine($"Skipped {Path.GetFileName(path)}: {ex.Message}"); }
            }
            Console.WriteLine($"Cleared {deleted} of {files.Length} file(s) from {dir}.");
            return 0;
        });
        return clear;
    }
}
