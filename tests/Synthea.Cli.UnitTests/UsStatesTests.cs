using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class UsStatesTests
{
    [Theory]
    [InlineData("OH", "Ohio")]
    [InlineData("oh", "Ohio")]
    [InlineData("Oh", "Ohio")]
    [InlineData("MA", "Massachusetts")]
    [InlineData("DC", "District of Columbia")]
    [InlineData("PR", "Puerto Rico")]
    [InlineData("VI", "Virgin Islands")]
    [InlineData("MP", "Northern Mariana Islands")]
    public void Normalize_TwoLetterCode_ReturnsFullName(string input, string expected)
    {
        Assert.Equal(expected, UsStates.Normalize(input));
    }

    [Theory]
    [InlineData("Ohio")]
    [InlineData("Massachusetts")]
    [InlineData("New Hampshire")]
    [InlineData("Puerto Rico")]
    public void Normalize_FullName_ReturnsUnchanged(string input)
    {
        Assert.Equal(input, UsStates.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrEmpty_ReturnsNull(string? input)
    {
        Assert.Null(UsStates.Normalize(input));
    }

    [Fact]
    public void Normalize_UnknownFullName_PassesThrough()
    {
        // Synthea owns the place-existence check; the CLI does not
        // second-guess names of length >= 3.
        Assert.Equal("Atlantis", UsStates.Normalize("Atlantis"));
    }

    [Theory]
    [InlineData("OH", true)]
    [InlineData("oh", true)]
    [InlineData("MP", true)]
    [InlineData("ZZ", false)]
    [InlineData("XX", false)]
    [InlineData("OHO", false)] // 3 letters — not a code
    [InlineData("O", false)]   // 1 letter — not a code
    public void IsKnownCode_ReturnsExpected(string input, bool expected)
    {
        Assert.Equal(expected, UsStates.IsKnownCode(input));
    }

    [Fact]
    public void CodeToName_Has50StatesPlusDcPlusFiveTerritories()
    {
        // 50 states + DC + 5 territories (PR, VI, GU, AS, MP) = 56.
        Assert.Equal(56, UsStates.CodeToName.Count);
    }
}
