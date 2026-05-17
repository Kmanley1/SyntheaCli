using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class SyntheaErrorPatternsTests
{
    [Fact]
    public void TryGetRemediation_UnknownState_ReturnsGeographyHint()
    {
        var stderr = "java.lang.RuntimeException: Unable to select a random city id for state Zoo\n  at App.run(App.java:42)";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.NotNull(hint);
        Assert.Contains("geography", hint, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetRemediation_Oom_ReturnsHeapHint()
    {
        var stderr = "Exception in thread \"main\" java.lang.OutOfMemoryError: Java heap space";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.NotNull(hint);
        Assert.Contains("heap", hint, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetRemediation_OldJava_ReturnsJavaUpgradeHint()
    {
        var stderr = "Error: LinkageError occurred while loading main class App\n  java.lang.UnsupportedClassVersionError: App has been compiled by a more recent version";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.NotNull(hint);
        Assert.Contains("Java", hint);
    }

    [Fact]
    public void TryGetRemediation_NoClassDefFound_ReturnsCorruptJarHint()
    {
        var stderr = "Exception in thread \"main\" java.lang.NoClassDefFoundError: org/mitre/synthea/X";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.NotNull(hint);
        Assert.Contains("cache clear", hint, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetRemediation_FileNotFound_ReturnsInputPathsHint()
    {
        var stderr = "Caused by: java.io.FileNotFoundException: /not/here.json";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.NotNull(hint);
        Assert.Contains("--module", hint);
    }

    [Fact]
    public void TryGetRemediation_UnknownError_ReturnsNull()
    {
        var stderr = "java.lang.NullPointerException: \"this.x\" is null because this.thing is null";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.Null(hint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryGetRemediation_EmptyOrNull_ReturnsNull(string? stderr)
    {
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.Null(hint);
    }

    [Fact]
    public void TryGetRemediation_Match_IsCaseInsensitive()
    {
        var stderr = "exception in thread main java.lang.outofmemoryerror: heap";
        var hint = SyntheaErrorPatterns.TryGetRemediation(stderr);
        Assert.NotNull(hint);
    }
}
