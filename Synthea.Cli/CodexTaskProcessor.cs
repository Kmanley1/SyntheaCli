using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Synthea.Cli;

public interface ITaskImplementer
{
    /// <summary>
    /// Returns true if the task represented by the markdown file has already been completed.
    /// </summary>
    bool IsTaskCompleted(string filePath);

    /// <summary>
    /// Implements the task represented by the markdown file. Returns true on success.
    /// </summary>
    bool ImplementTask(string filePath);
}

public static class CodexTaskProcessor
{
    public static void ProcessTasks(string sourceDir, string targetDir, ITaskImplementer implementer)
    {
        if (sourceDir == null) throw new ArgumentNullException(nameof(sourceDir));
        if (targetDir == null) throw new ArgumentNullException(nameof(targetDir));
        if (implementer == null) throw new ArgumentNullException(nameof(implementer));

        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);

        var contextDir = Path.Combine(sourceDir, "context");
        Directory.CreateDirectory(contextDir);

        var preDir = Path.Combine(contextDir, "pre");
        var postDir = Path.Combine(contextDir, "post");

        if (!Directory.Exists(preDir))
        {
            Console.WriteLine($"Creating missing pre-task directory: {preDir}");
        }
        if (!Directory.Exists(postDir))
        {
            Console.WriteLine($"Creating missing post-task directory: {postDir}");
        }

        // ensure context subdirectories exist so automation continues even if
        // repository is missing them. This mirrors 'mkdir -p' behaviour.
        Directory.CreateDirectory(preDir);
        Directory.CreateDirectory(postDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.md", SearchOption.TopDirectoryOnly).OrderBy(f => f))
        {
            var startUtc = DateTime.UtcNow;
            var name = Path.GetFileName(file);

            var preFiles = Directory.Exists(preDir)
                ? Directory.EnumerateFiles(preDir, "*.md").OrderBy(f => f)
                : Enumerable.Empty<string>();
            foreach (var pre in preFiles)
            {
                var pn = Path.GetFileName(pre);
                try
                {
                    Console.WriteLine($"Processing pre-task: {pn}");
                    implementer.ImplementTask(pre);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing {pn}: {ex.Message}");
                }
            }

            try
            {
                Console.WriteLine($"Processing task: {name}");
                if (implementer.IsTaskCompleted(file))
                {
                    Console.WriteLine($"Task already completed: {name}");
                }
                else if (implementer.ImplementTask(file))
                {
                    var prefix = startUtc.ToString("yyyy-MM-dd_HH-mm-ss");
                    var destName = name;
                    if (!Regex.IsMatch(name, "^\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}-"))
                    {
                        destName = $"{prefix}-{name}";
                    }
                    var dest = Path.Combine(targetDir, destName);
                    File.Move(file, dest, overwrite: true);
                    Console.WriteLine($"Task completed and file moved: {destName}");
                }
                else
                {
                    Console.Error.WriteLine($"Failed to implement task: {name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {name}: {ex.Message}");
            }
            finally
            {
                var postFiles = Directory.Exists(postDir)
                    ? Directory.EnumerateFiles(postDir, "*.md").OrderBy(f => f)
                    : Enumerable.Empty<string>();
                foreach (var post in postFiles)
                {
                    var pn = Path.GetFileName(post);
                    try
                    {
                        Console.WriteLine($"Processing post-task: {pn}");
                        implementer.ImplementTask(post);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error processing {pn}: {ex.Message}");
                    }
                }
            }
        }
    }
}
