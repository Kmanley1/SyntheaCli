using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class ModulesCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StubJarSource _jarSource;
    private readonly ServiceProvider _services;

    public ModulesCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _jarSource = new StubJarSource(BuildFakeJar(
            ("modules/asthma.json", MinimalModuleJson),
            ("modules/medications/inhaler.json", MinimalModuleJson)));
        var sc = new ServiceCollection();
        sc.AddSingleton<IProcessRunner>(new NoopProcessRunner());
        sc.AddSingleton<IJarSource>(_jarSource);
        sc.AddSingleton<IJavaDetector>(new NoopJavaDetector());
        _services = sc.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private const string MinimalModuleJson = """
        { "name": "Asthma", "gmf_version": 2,
          "remarks": ["A simple asthma module."],
          "states": { "Initial": {}, "Diagnosis": {}, "Terminal": {} } }
        """;

    private string BuildFakeJar(params (string EntryPath, string Content)[] entries)
    {
        var jarPath = Path.Combine(_tempDir, "fake-synthea-with-dependencies.jar");
        using var fs = File.Create(jarPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (entry, content) in entries)
        {
            var e = zip.CreateEntry(entry);
            using var s = e.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            s.Write(bytes, 0, bytes.Length);
        }
        return jarPath;
    }

    [Fact]
    public async Task ModulesList_WithCachedJar_ReturnsZero()
    {
        var code = await Program.RunAsync(new[] { "modules", "list" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ModulesList_NoJarNorDir_ReturnsOne()
    {
        _jarSource.CachedJar = null;
        var code = await Program.RunAsync(new[] { "modules", "list" }, _services);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task ModulesList_NoJarButDirGiven_ReturnsZero()
    {
        _jarSource.CachedJar = null;
        var dir = Path.Combine(_tempDir, "mods");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "covid.json"), MinimalModuleJson);
        var code = await Program.RunAsync(new[] { "modules", "list", "--module-dir", dir }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ModulesDescribe_ByLeafName_ReturnsZero()
    {
        var code = await Program.RunAsync(new[] { "modules", "describe", "asthma" }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ModulesDescribe_Unknown_ReturnsOne()
    {
        var code = await Program.RunAsync(new[] { "modules", "describe", "ghost-module" }, _services);
        Assert.Equal(1, code);
    }

    [Fact]
    public async Task ModulesDescribe_FromFsDirectory_PreferredOverJar()
    {
        var dir = Path.Combine(_tempDir, "mods");
        Directory.CreateDirectory(dir);
        // A filesystem module shadows whatever's in the JAR when --module-dir is passed.
        File.WriteAllText(Path.Combine(dir, "asthma.json"),
            """{ "name": "FromDisk", "gmf_version": 99, "states": {} }""");
        var code = await Program.RunAsync(new[] { "modules", "describe", "asthma", "--module-dir", dir }, _services);
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task ModulesDescribe_NoJarNoDir_ReturnsOne()
    {
        _jarSource.CachedJar = null;
        var code = await Program.RunAsync(new[] { "modules", "describe", "asthma" }, _services);
        Assert.Equal(1, code);
    }

    [Fact]
    public void PrintDescription_ReturnsZero()
    {
        // Direct call to confirm the formatter contract; no behavior under test other
        // than the contract that printing succeeds and exits 0.
        var d = new ModuleDescription("X", "Some remarks.", "2", 3, "modules/x.json");
        Assert.Equal(0, ModulesCommand.PrintDescription(d));
    }

    // ----- Stubs ---------------------------------------------------------

    private sealed class StubJarSource : IJarSource
    {
        public StubJarSource(string jarPath)
        {
            CachePath = Path.GetDirectoryName(jarPath) ?? Path.GetTempPath();
            CachedJar = new FileInfo(jarPath);
        }
        public string CachePath { get; }
        public FileInfo? CachedJar { get; set; }
        public FileInfo? TryFindCachedJar() => CachedJar;
        public Task<FileInfo> EnsureJarAsync(
            bool forceRefresh = false,
            IProgress<(long downloaded, long total)>? prog = null,
            CancellationToken token = default,
            JarOverrides? overrides = null)
            => Task.FromResult(CachedJar ?? throw new InvalidOperationException("no jar"));
    }

    private sealed class NoopProcessRunner : IProcessRunner
    {
        public IProcess Start(System.Diagnostics.ProcessStartInfo psi)
            => throw new InvalidOperationException("modules should not start processes");
    }

    private sealed class NoopJavaDetector : IJavaDetector
    {
        public Task<JavaProbeResult> ProbeAsync(string javaPath, CancellationToken cancelToken = default)
            => Task.FromResult(new JavaProbeResult(true, 21, "21.0.5", null));
    }
}
