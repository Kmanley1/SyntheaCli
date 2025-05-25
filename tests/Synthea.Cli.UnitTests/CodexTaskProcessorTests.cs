using System;
using System.IO;
using Xunit;
using System.Collections.Generic;
using Synthea.Cli;
using System.Text.RegularExpressions;
using System.Linq;

namespace Synthea.Cli.UnitTests;

public class CodexTaskProcessorTests : IDisposable
{
    private readonly string _src;
    private readonly string _dest;
    private readonly string _preDir;
    private readonly string _postDir;

    public CodexTaskProcessorTests()
    {
        _src = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_dest);
        var ctxRoot = Path.Combine(_src, "context");
        _preDir = Path.Combine(ctxRoot, "pre");
        _postDir = Path.Combine(ctxRoot, "post");
        Directory.CreateDirectory(_preDir);
        Directory.CreateDirectory(_postDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_src, true); } catch { }
        try { Directory.Delete(_dest, true); } catch { }
    }

    [Fact]
    public void MovesFileOnSuccessfulImplementation()
    {
        var file = Path.Combine(_src, "task1.md");
        File.WriteAllText(file, "task content");

        var impl = new StubImplementer(success: true);
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);

        Assert.False(File.Exists(file));
        var moved = Directory.GetFiles(_dest).Single();
        Assert.Matches("^\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}-task1\\.md$", Path.GetFileName(moved));
        Assert.Equal(new[] { file }, impl.Implemented);
    }

    [Fact]
    public void LeavesFileOnFailedImplementation()
    {
        var file = Path.Combine(_src, "task2.md");
        File.WriteAllText(file, "task content");

        var impl = new StubImplementer(success: false);
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);

        Assert.True(File.Exists(file));
        Assert.Empty(Directory.GetFiles(_dest));
    }

    [Fact]
    public void ExecutesPreAndPostTasksAroundEachTask()
    {
        var preFile = Path.Combine(_preDir, "pre.md");
        File.WriteAllText(preFile, "p");
        var postFile = Path.Combine(_postDir, "post.md");
        File.WriteAllText(postFile, "p");
        var taskFile = Path.Combine(_src, "task.md");
        File.WriteAllText(taskFile, "t");

        var impl = new StubImplementer(success: true);
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);

        Assert.Equal(new[] { preFile, taskFile, postFile }, impl.Implemented);
        var moved = Directory.GetFiles(_dest).Single();
        Assert.Matches("^\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}-task\\.md$", Path.GetFileName(moved));
    }

    [Fact]
    public void ContextFilesAreNotMoved()
    {
        var preFile = Path.Combine(_preDir, "a.md");
        File.WriteAllText(preFile, "p");
        var postFile = Path.Combine(_postDir, "b.md");
        File.WriteAllText(postFile, "p");

        var impl = new StubImplementer(success: true);
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);

        Assert.True(File.Exists(preFile));
        Assert.True(File.Exists(postFile));
        Assert.False(File.Exists(Path.Combine(_dest, "a.md")));
        Assert.False(File.Exists(Path.Combine(_dest, "b.md")));
        Assert.Empty(Directory.GetFiles(_dest));
    }

    [Fact]
    public void CreatesMissingContextDirsAndMovesFile()
    {
        Directory.Delete(_preDir, true);
        Directory.Delete(_postDir, true);
        var file = Path.Combine(_src, "task.md");
        File.WriteAllText(file, "t");

        var impl = new StubImplementer(success: true);
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);

        Assert.True(Directory.Exists(_preDir));
        Assert.True(Directory.Exists(_postDir));

        Assert.False(File.Exists(file));
        var moved = Directory.GetFiles(_dest).Single();
        Assert.Matches("^\\d{4}-\\d{2}-\\d{2}_\\d{2}-\\d{2}-\\d{2}-task\\.md$", Path.GetFileName(moved));
    }

    [Fact]
    public void DoesNotPrefixAlreadyTimestampedFile()
    {
        var name = "2025-01-01_00-00-00-task.md";
        var file = Path.Combine(_src, name);
        File.WriteAllText(file, "t");

        var impl = new StubImplementer(success: true);
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);

        Assert.True(File.Exists(Path.Combine(_dest, name)));
        Assert.Empty(Directory.GetFiles(_src));

        // second run should not modify the file
        CodexTaskProcessor.ProcessTasks(_src, _dest, impl);
        Assert.Single(Directory.GetFiles(_dest));
    }

    private class StubImplementer : ITaskImplementer
    {
        private readonly bool _success;
        public readonly List<string> Implemented = new();
        public StubImplementer(bool success) => _success = success;
        public bool IsTaskCompleted(string filePath) => false;
        public bool ImplementTask(string filePath)
        {
            Implemented.Add(filePath);
            return _success;
        }
    }
}
