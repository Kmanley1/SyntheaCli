using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class SyntheaProgressParserTests
{
    [Fact]
    public void TryConsume_RecognizesPatientLine_AdvancesCount()
    {
        var p = new SyntheaProgressParser();
        Assert.True(p.TryConsume("1 -- Bob (45 y/o M) Cleveland, Ohio"));
        Assert.Equal(1, p.LastCount);
    }

    [Fact]
    public void TryConsume_AdvancesAcrossMultipleLines()
    {
        var p = new SyntheaProgressParser();
        p.TryConsume("1 -- Bob (45 y/o M) Cleveland, Ohio");
        p.TryConsume("2 -- Alice (32 y/o F) Akron, Ohio");
        p.TryConsume("3 -- Carol (67 y/o F) Toledo, Ohio");
        Assert.Equal(3, p.LastCount);
    }

    [Fact]
    public void TryConsume_PaddedInteger_StillMatches()
    {
        // Synthea pads the count column once the run is wider than one digit:
        // " 9 -- ...", " 10 -- ...", "  99 -- ...". Pre-anchor whitespace must
        // not break recognition.
        var p = new SyntheaProgressParser();
        Assert.True(p.TryConsume("  99 -- Dave (50 y/o M) Cincinnati, Ohio"));
        Assert.Equal(99, p.LastCount);
    }

    [Fact]
    public void TryConsume_LowerCountAfterHigher_IsIgnored()
    {
        // Threaded generation can emit lines out of order; once we've
        // seen N we never regress. The line is still classified as
        // "a progress line", we just don't update state.
        var p = new SyntheaProgressParser();
        p.TryConsume("5 -- E (10 y/o F) X, Ohio");
        Assert.True(p.TryConsume("3 -- F (20 y/o M) Y, Ohio"));
        Assert.Equal(5, p.LastCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Exception in thread \"main\" java.lang.NullPointerException")]
    [InlineData("Loading modules...")]
    [InlineData("[INFO] starting generation")]
    [InlineData("-- separator --")]
    public void TryConsume_NonProgressLine_ReturnsFalse(string line)
    {
        var p = new SyntheaProgressParser();
        Assert.False(p.TryConsume(line));
        Assert.Equal(0, p.LastCount);
    }

    [Fact]
    public void TryConsume_NullLine_ReturnsFalse()
    {
        var p = new SyntheaProgressParser();
        Assert.False(p.TryConsume(null!));
        Assert.Equal(0, p.LastCount);
    }
}
