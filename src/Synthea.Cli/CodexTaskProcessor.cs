using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;

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
        var stagedDir = Path.Combine(sourceDir, "staged");
        Directory.CreateDirectory(stagedDir);

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
                else
                {
                    var prefix = startUtc.ToString("yyyy-MM-dd_HH-mm-ss");
                    var baseName = Path.GetFileNameWithoutExtension(name).Replace(' ', '_');
                    var logName = $"{prefix}-{baseName}-log.md";
                    var fbName  = $"{prefix}-{baseName}-feedback.md";
                    var logPath = GetUniqueFilePath(stagedDir, logName);
                    var fbPath  = GetUniqueFilePath(stagedDir, fbName);

                    using var outBuf = new StringWriter();
                    using var errBuf = new StringWriter();
                    var origOut = Console.Out;
                    var origErr = Console.Error;
                    Console.SetOut(new TeeTextWriter(origOut, outBuf));
                    Console.SetError(new TeeTextWriter(origErr, errBuf));
                    var sw = Stopwatch.StartNew();
                    var success = false;
                    try
                    {
                        success = implementer.ImplementTask(file);
                    }
                    finally
                    {
                        sw.Stop();
                        Console.SetOut(origOut);
                        Console.SetError(origErr);
                    }

                    var logs = outBuf.ToString() + errBuf.ToString();
                    WriteLogFile(logPath, logs);
                    WriteFeedbackFile(fbPath, logs, sw.Elapsed);
                    InsertPointer(file, Path.GetFileName(logPath), Path.GetFileName(fbPath));

                    if (success)
                    {
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

    private static void WriteLogFile(string path, string logs)
    {
        var ansi = new Regex("\x1B\\[[0-?]*[ -/]*[@-~]");
        var lines = logs.Split('\n').Select(l => ansi.Replace(l, string.Empty)).ToList();
        if (lines.Count > 200) lines = lines.Skip(lines.Count - 200).ToList();
        var content = $"```text\n{string.Join('\n', lines)}\n```";
        File.WriteAllText(path, content);
    }

    private static void WriteFeedbackFile(string path, string logs, TimeSpan dur)
    {
        var ansi = new Regex("\x1B\\[[0-?]*[ -/]*[@-~]");
        var lines = logs.Split('\n').Select(l => ansi.Replace(l, string.Empty)).ToList();
        var warn = lines.Count(l => l.Contains("WARN", StringComparison.OrdinalIgnoreCase));
        var err = lines.Count(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase));
        var content = $"- Duration: {dur:c}\n- WARN lines: {warn}\n- ERROR lines: {err}\n";
        File.WriteAllText(path, content);
    }

    private static void InsertPointer(string file, string logName, string fbName)
    {
        var lines = File.ReadAllLines(file).ToList();
        lines.Add("");
        lines.Add("## Post‑run Artefacts");
        var logRel = Path.Combine("tasks", "staged", logName).Replace('\\', '/');
        var fbRel = Path.Combine("tasks", "staged", fbName).Replace('\\', '/');
        lines.Add($"- [Execution Log](../../{logRel})");
        lines.Add($"- [Codex Feedback](../../{fbRel})");
        File.WriteAllLines(file, lines);
    }

    private static string GetUniqueFilePath(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        if (!File.Exists(path)) return path;
        var baseName = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var v = 2;
        while (File.Exists(Path.Combine(dir, $"{baseName}-v{v}{ext}")))
            v++;
        return Path.Combine(dir, $"{baseName}-v{v}{ext}");
    }

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _a;
        private readonly TextWriter _b;
        public TeeTextWriter(TextWriter a, TextWriter b) { _a = a; _b = b; }
        public override Encoding Encoding => _a.Encoding;
        public override void Write(char value) { _a.Write(value); _b.Write(value); }
        public override void Write(string? value) { _a.Write(value); _b.Write(value); }
        public override void WriteLine(string? value) { _a.WriteLine(value); _b.WriteLine(value); }
        public override void Flush() { _a.Flush(); _b.Flush(); }
    }
}
