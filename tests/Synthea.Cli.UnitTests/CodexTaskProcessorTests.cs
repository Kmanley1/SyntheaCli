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

    public CodexTaskProcessorTests()
    {
        _src = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _dest = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_dest);
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
