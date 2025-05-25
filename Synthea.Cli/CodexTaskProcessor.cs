using System;
using System.IO;
using System.Linq;

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
        if (!Directory.Exists(contextDir))
        {
            throw new DirectoryNotFoundException($"Context directory not found: {contextDir}");
        }

        var preDir = Path.Combine(contextDir, "pre");
        var postDir = Path.Combine(contextDir, "post");

        if (!Directory.Exists(preDir))
        {
            throw new DirectoryNotFoundException($"Pre-task directory not found: {preDir}");
        }

        if (!Directory.Exists(postDir))
        {
            throw new DirectoryNotFoundException($"Post-task directory not found: {postDir}");
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.md", SearchOption.TopDirectoryOnly).OrderBy(f => f))
        {
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
                    var dest = Path.Combine(targetDir, name);
                    File.Move(file, dest, overwrite: true);
                    Console.WriteLine($"Task completed and file moved: {name}");
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
