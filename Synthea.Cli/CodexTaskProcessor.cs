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

        foreach (var file in Directory.EnumerateFiles(contextDir, "*.md").OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            try
            {
                Console.WriteLine($"Processing context task: {name}");
                implementer.ImplementTask(file);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing {name}: {ex.Message}");
            }
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.md", SearchOption.TopDirectoryOnly).OrderBy(f => f))
        {
            var name = Path.GetFileName(file);
            try
            {
                Console.WriteLine($"Processing task: {name}");
                if (implementer.IsTaskCompleted(file))
                {
                    Console.WriteLine($"Task already completed: {name}");
                    continue;
                }
                var success = implementer.ImplementTask(file);
                if (success)
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
        }
    }
}
