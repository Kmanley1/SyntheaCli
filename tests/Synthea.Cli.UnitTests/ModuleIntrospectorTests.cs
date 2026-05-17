using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class ModuleIntrospectorTests : IDisposable
{
    private readonly string _tempDir;

    public ModuleIntrospectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string BuildFakeJar(params (string EntryPath, string Content)[] entries)
    {
        var jarPath = Path.Combine(_tempDir, "fake-synthea-with-dependencies.jar");
        using (var fs = File.Create(jarPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var (entry, content) in entries)
            {
                var e = zip.CreateEntry(entry);
                using var s = e.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
        }
        return jarPath;
    }

    private const string MinimalModuleJson = """
        { "name": "Asthma", "gmf_version": 2, "remarks": ["First line.", "Second line."],
          "states": { "Initial": {}, "Diagnosis": {}, "Terminal": {} } }
        """;

    [Fact]
    public void ListJarModules_FiltersToModulesJson()
    {
        var jar = BuildFakeJar(
            ("modules/asthma.json", MinimalModuleJson),
            ("modules/medications/inhaler.json", MinimalModuleJson),
            ("modules/README.txt", "not a module"),
            ("META-INF/MANIFEST.MF", "manifest"),
            ("config.properties", "x=y"));
        var list = ModuleIntrospector.ListJarModules(jar);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.Name == "asthma" && e.Location == "modules/asthma.json");
        Assert.Contains(list, e => e.Location == "modules/medications/inhaler.json");
        Assert.All(list, e => Assert.Equal(ModuleSource.Jar, e.Source));
    }

    [Fact]
    public void ListJarModules_ResultsAreSortedByName()
    {
        var jar = BuildFakeJar(
            ("modules/zoster.json", MinimalModuleJson),
            ("modules/asthma.json", MinimalModuleJson),
            ("modules/diabetes.json", MinimalModuleJson));
        var names = ModuleIntrospector.ListJarModules(jar).Select(e => e.Name).ToArray();
        Assert.Equal(new[] { "asthma", "diabetes", "zoster" }, names);
    }

    [Fact]
    public void ListJarModules_MissingJar_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ModuleIntrospector.ListJarModules(Path.Combine(_tempDir, "nope.jar")));
    }

    [Fact]
    public void ListJarModules_CachesAcrossCalls()
    {
        var jar = BuildFakeJar(("modules/asthma.json", MinimalModuleJson));
        var cacheDir = Path.Combine(_tempDir, "cache");
        var first = ModuleIntrospector.ListJarModules(jar, cacheDir);
        Assert.Single(first);

        // Cache file should now exist with the sha-prefixed name.
        var sha = ModuleIntrospector.ComputeJarShaPrefix(jar);
        var cachePath = Path.Combine(cacheDir, $"modules-cache-{sha}.json");
        Assert.True(File.Exists(cachePath));

        // Delete the JAR — if caching truly bypassed the zip walk, this
        // still returns the cached list.
        File.Delete(jar);
        Assert.Throws<FileNotFoundException>(() => ModuleIntrospector.ListJarModules(jar, cacheDir));
        // Restore + re-read cache transparently.
        BuildFakeJar(("modules/asthma.json", MinimalModuleJson));
        var second = ModuleIntrospector.ListJarModules(jar, cacheDir);
        Assert.Single(second);
    }

    [Fact]
    public void ListJarModules_CacheInvalidatedWhenShaChanges()
    {
        var jar = BuildFakeJar(("modules/asthma.json", MinimalModuleJson));
        var cacheDir = Path.Combine(_tempDir, "cache");
        var sha1 = ModuleIntrospector.ComputeJarShaPrefix(jar);
        _ = ModuleIntrospector.ListJarModules(jar, cacheDir);

        // Replace JAR with a different content set → different SHA → cache miss.
        File.Delete(jar);
        BuildFakeJar(
            ("modules/asthma.json", MinimalModuleJson),
            ("modules/diabetes.json", MinimalModuleJson));
        var sha2 = ModuleIntrospector.ComputeJarShaPrefix(jar);
        Assert.NotEqual(sha1, sha2);
        var list = ModuleIntrospector.ListJarModules(jar, cacheDir);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void ListDirectoryModules_RecursivelyFindsJson()
    {
        var dir = Path.Combine(_tempDir, "mods");
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        File.WriteAllText(Path.Combine(dir, "asthma.json"), MinimalModuleJson);
        File.WriteAllText(Path.Combine(dir, "sub", "covid.json"), MinimalModuleJson);
        File.WriteAllText(Path.Combine(dir, "notes.txt"), "ignored");
        var list = ModuleIntrospector.ListDirectoryModules(dir);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, e => e.Name == "asthma");
        Assert.Contains(list, e => e.Name.EndsWith("covid"));
        Assert.All(list, e => Assert.Equal(ModuleSource.Directory, e.Source));
    }

    [Fact]
    public void DescribeJarModule_ByLeafName_ReturnsParsedFields()
    {
        var jar = BuildFakeJar(("modules/asthma.json", MinimalModuleJson));
        var d = ModuleIntrospector.DescribeJarModule(jar, "asthma");
        Assert.Equal("Asthma", d.Name);
        Assert.Equal("2", d.GmfVersion);
        Assert.Equal(3, d.StateCount);
        Assert.Contains("First line.", d.Remarks);
        Assert.Equal("modules/asthma.json", d.Location);
    }

    [Fact]
    public void DescribeJarModule_ByFullEntryPath_Works()
    {
        var jar = BuildFakeJar(("modules/sub/covid.json", MinimalModuleJson));
        var d = ModuleIntrospector.DescribeJarModule(jar, "modules/sub/covid.json");
        Assert.Equal("modules/sub/covid.json", d.Location);
    }

    [Fact]
    public void DescribeJarModule_Unknown_Throws()
    {
        var jar = BuildFakeJar(("modules/asthma.json", MinimalModuleJson));
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ModuleIntrospector.DescribeJarModule(jar, "ghostmodule"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void DescribeJarModule_AmbiguousLeaf_Throws()
    {
        var jar = BuildFakeJar(
            ("modules/a/dup.json", MinimalModuleJson),
            ("modules/b/dup.json", MinimalModuleJson));
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ModuleIntrospector.DescribeJarModule(jar, "dup"));
        Assert.Contains("ambiguous", ex.Message);
    }

    [Fact]
    public void DescribeFileModule_ParsesScalarRemarks()
    {
        var path = Path.Combine(_tempDir, "scalar.json");
        File.WriteAllText(path, """{ "name": "Cold", "remarks": "Just a string.", "states": {"A": {}} }""");
        var d = ModuleIntrospector.DescribeFileModule(path);
        Assert.Equal("Cold", d.Name);
        Assert.Equal("Just a string.", d.Remarks);
        Assert.Equal(1, d.StateCount);
    }

    [Fact]
    public void DescribeFileModule_NoName_FallsBackToFilename()
    {
        var path = Path.Combine(_tempDir, "no-name.json");
        File.WriteAllText(path, """{ "states": {} }""");
        var d = ModuleIntrospector.DescribeFileModule(path);
        Assert.NotEqual(string.Empty, d.Name);
        Assert.Equal(0, d.StateCount);
        Assert.Null(d.GmfVersion);
    }
}
