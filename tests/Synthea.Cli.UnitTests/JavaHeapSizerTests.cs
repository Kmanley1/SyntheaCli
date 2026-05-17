using System.IO;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class JavaHeapSizerTests
{
    [Theory]
    [InlineData(null, null)]   // no population → JVM default
    [InlineData(1, null)]
    [InlineData(999, null)]
    [InlineData(1000, "-Xmx2g")]
    [InlineData(9999, "-Xmx2g")]
    [InlineData(10_000, "-Xmx4g")]
    [InlineData(99_999, "-Xmx4g")]
    [InlineData(100_000, "-Xmx8g")]
    [InlineData(999_999, "-Xmx8g")]
    [InlineData(1_000_000, "-Xmx16g")]
    [InlineData(5_000_000, "-Xmx16g")]
    public void Suggest_HitsExpectedTier(int? pop, string? expected)
    {
        Assert.Equal(expected, JavaHeapSizer.Suggest(pop));
    }

    [Theory]
    [InlineData("4g", "-Xmx4g")]
    [InlineData("1024m", "-Xmx1024m")]
    [InlineData("-Xmx8g", "-Xmx8g")]        // already prefixed → kept as-is
    [InlineData("  2g  ", "-Xmx2g")]        // forgive padding
    public void Resolve_OverrideWinsOverSuggestion(string overrideValue, string expected)
    {
        // Population would suggest -Xmx16g but the override must win.
        var resolved = JavaHeapSizer.Resolve(overrideValue, 5_000_000);
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Resolve_NoOverride_UsesSuggestion()
    {
        Assert.Equal("-Xmx4g", JavaHeapSizer.Resolve(null, 50_000));
    }

    [Fact]
    public void Resolve_NoOverrideAndSmallPop_ReturnsNull()
    {
        Assert.Null(JavaHeapSizer.Resolve(null, 100));
    }

    [Theory]
    [InlineData("4g", null)]
    [InlineData("8G", null)]
    [InlineData("1024m", null)]
    [InlineData("1024M", null)]
    public void ValidateOverride_AcceptsCanonicalShapes(string input, string? expected)
    {
        Assert.Equal(expected, JavaHeapSizer.ValidateOverride(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("g")]
    [InlineData("4")]
    [InlineData("4gb")]
    [InlineData("4 g")]
    public void ValidateOverride_RejectsBadShapes(string input)
    {
        Assert.NotNull(JavaHeapSizer.ValidateOverride(input));
    }

    // ---- CreateProcessStartInfo integration -----------------------------
    //
    // The heap arg must precede `-jar` in the PSI ArgumentList because the
    // JVM treats anything after -jar as application arguments, not JVM args.

    [Fact]
    public void CreateProcessStartInfo_HeapSuggestion_PrecedesJarFlag()
    {
        var args = MinimalArgs(population: 50_000);
        var psi = RunCommand.CreateProcessStartInfo(MinimalHosting(), args,
            new FileInfo(Path.Combine(Path.GetTempPath(), "synthea.jar")));
        var list = psi.ArgumentList;
        var xmxIdx = list.IndexOf("-Xmx4g");
        var jarIdx = list.IndexOf("-jar");
        Assert.True(xmxIdx >= 0, "expected -Xmx4g for 50k population");
        Assert.True(jarIdx >= 0);
        Assert.True(xmxIdx < jarIdx, $"-Xmx ({xmxIdx}) must precede -jar ({jarIdx})");
    }

    [Fact]
    public void CreateProcessStartInfo_HeapOverride_BeatsSuggestion()
    {
        var psi = RunCommand.CreateProcessStartInfo(
            MinimalHosting(), MinimalArgs(population: 50_000),
            new FileInfo(Path.Combine(Path.GetTempPath(), "synthea.jar")),
            heapOverride: "12g");
        Assert.Contains("-Xmx12g", psi.ArgumentList);
        Assert.DoesNotContain("-Xmx4g", psi.ArgumentList);
    }

    [Fact]
    public void CreateProcessStartInfo_SmallPopNoOverride_NoHeap()
    {
        var psi = RunCommand.CreateProcessStartInfo(
            MinimalHosting(), MinimalArgs(population: 50),
            new FileInfo(Path.Combine(Path.GetTempPath(), "synthea.jar")));
        Assert.DoesNotContain(psi.ArgumentList, a => a.StartsWith("-Xmx"));
    }

    [Fact]
    public void CreateProcessStartInfo_OverrideHonoredAtLowPop()
    {
        // Even at sub-1000 population, an explicit override is honored.
        var psi = RunCommand.CreateProcessStartInfo(
            MinimalHosting(), MinimalArgs(population: 50),
            new FileInfo(Path.Combine(Path.GetTempPath(), "synthea.jar")),
            heapOverride: "1024m");
        Assert.Contains("-Xmx1024m", psi.ArgumentList);
    }

    private static HostingOptions MinimalHosting()
        => new(new DirectoryInfo(Path.GetTempPath()), Refresh: false, JavaPath: "java");

    private static SyntheaArgs MinimalArgs(int? population)
        => new(
            State: null, City: null, Gender: null, AgeRange: null,
            ModuleDir: null, Modules: null, Population: population, Seed: null,
            Config: null, Zip: null, FhirVersion: null,
            InitialSnapshot: null, UpdatedSnapshot: null, DaysForward: null,
            Formats: System.Array.Empty<string>(),
            AdditionalFormats: System.Array.Empty<string>(),
            Passthru: System.Array.Empty<string>());
}
