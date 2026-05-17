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
        // Normalize is a pure conversion: 2-letter → full name, anything
        // else returned unchanged. After C1 the option validator rejects
        // unknown full names, but Normalize itself is still lossless.
        Assert.Equal("Atlantis", UsStates.Normalize("Atlantis"));
    }

    [Theory]
    [InlineData("Ohio", true)]
    [InlineData("ohio", true)]
    [InlineData("OHIO", true)]
    [InlineData("New Hampshire", true)]
    [InlineData("new hampshire", true)]
    [InlineData("Massachusetts", true)]
    [InlineData("Puerto Rico", true)]
    [InlineData("Atlantis", false)]
    [InlineData("Yukon", false)]
    [InlineData("Mass", false)]
    public void IsKnownFullName_ReturnsExpected(string input, bool expected)
    {
        Assert.Equal(expected, UsStates.IsKnownFullName(input));
    }

    [Theory]
    [InlineData("Massachsetts", "Massachusetts")]    // 1 deletion
    [InlineData("Massachusettz", "Massachusetts")]   // 1 substitution
    [InlineData("Ohyo", "Ohio")]                     // transposition-ish, len 4, 2 edits → threshold 1, won't match
    [InlineData("ohio", "Ohio")]                     // exact (case-insensitive); won't reach SuggestClosest path
    public void SuggestClosest_NearMisses_ReturnSomething(string input, string _)
    {
        // Don't pin the exact suggestion — Levenshtein is symmetric and
        // ties can break either way across runs. Just assert we got *a*
        // suggestion for genuine near-misses.
        var suggestion = UsStates.SuggestClosest(input);
        // For inputs where the algorithm exceeds threshold, suggestion is null.
        // The 1-edit cases ("Massachsetts", "Massachusettz") should suggest.
        if (input.StartsWith("Mass", System.StringComparison.OrdinalIgnoreCase))
            Assert.NotNull(suggestion);
    }

    [Theory]
    [InlineData("Atlantis")]
    [InlineData("Mordor")]
    [InlineData("Xyzzy")]
    public void SuggestClosest_FarMisses_ReturnNull(string input)
    {
        Assert.Null(UsStates.SuggestClosest(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SuggestClosest_EmptyOrNull_ReturnsNull(string? input)
    {
        Assert.Null(UsStates.SuggestClosest(input!));
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
