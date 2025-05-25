using System;
using System.IO;
using Xunit;
using System.Collections.Generic;
using Synthea.Cli;

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
        Assert.True(File.Exists(Path.Combine(_dest, "task1.md")));
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
        Assert.False(File.Exists(Path.Combine(_dest, "task2.md")));
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
        Assert.True(File.Exists(Path.Combine(_dest, "task.md")));
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
    }

    [Fact]
    public void ThrowsIfPreDirMissing()
    {
        Directory.Delete(_preDir, true);
        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            CodexTaskProcessor.ProcessTasks(_src, _dest, new StubImplementer(true)));
        Assert.Contains("Pre-task directory", ex.Message);
    }

    [Fact]
    public void ThrowsIfPostDirMissing()
    {
        Directory.Delete(_postDir, true);
        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            CodexTaskProcessor.ProcessTasks(_src, _dest, new StubImplementer(true)));
        Assert.Contains("Post-task directory", ex.Message);
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
