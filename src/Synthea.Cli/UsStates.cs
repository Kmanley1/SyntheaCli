using System.Collections.Generic;

namespace Synthea.Cli;

/// <summary>
/// US-state lookup. Synthea's geography data is keyed by full state name
/// ("Ohio"), not by two-letter code ("OH"), so the CLI converts 2-letter
/// input to the full name before passthru. Full names are passed through
/// unchanged; Synthea remains the source of truth for whether a given
/// place exists in its dataset.
/// </summary>
internal static class UsStates
{
    /// <summary>
    /// Maps two-letter USPS codes (uppercase) to the full state/territory
    /// name that Synthea recognizes. Includes 50 states + DC + the five
    /// inhabited territories.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CodeToName = new Dictionary<string, string>
    {
        ["AL"] = "Alabama",
        ["AK"] = "Alaska",
        ["AZ"] = "Arizona",
        ["AR"] = "Arkansas",
        ["CA"] = "California",
        ["CO"] = "Colorado",
        ["CT"] = "Connecticut",
        ["DE"] = "Delaware",
        ["FL"] = "Florida",
        ["GA"] = "Georgia",
        ["HI"] = "Hawaii",
        ["ID"] = "Idaho",
        ["IL"] = "Illinois",
        ["IN"] = "Indiana",
        ["IA"] = "Iowa",
        ["KS"] = "Kansas",
        ["KY"] = "Kentucky",
        ["LA"] = "Louisiana",
        ["ME"] = "Maine",
        ["MD"] = "Maryland",
        ["MA"] = "Massachusetts",
        ["MI"] = "Michigan",
        ["MN"] = "Minnesota",
        ["MS"] = "Mississippi",
        ["MO"] = "Missouri",
        ["MT"] = "Montana",
        ["NE"] = "Nebraska",
        ["NV"] = "Nevada",
        ["NH"] = "New Hampshire",
        ["NJ"] = "New Jersey",
        ["NM"] = "New Mexico",
        ["NY"] = "New York",
        ["NC"] = "North Carolina",
        ["ND"] = "North Dakota",
        ["OH"] = "Ohio",
        ["OK"] = "Oklahoma",
        ["OR"] = "Oregon",
        ["PA"] = "Pennsylvania",
        ["RI"] = "Rhode Island",
        ["SC"] = "South Carolina",
        ["SD"] = "South Dakota",
        ["TN"] = "Tennessee",
        ["TX"] = "Texas",
        ["UT"] = "Utah",
        ["VT"] = "Vermont",
        ["VA"] = "Virginia",
        ["WA"] = "Washington",
        ["WV"] = "West Virginia",
        ["WI"] = "Wisconsin",
        ["WY"] = "Wyoming",
        ["DC"] = "District of Columbia",
        ["PR"] = "Puerto Rico",
        ["VI"] = "Virgin Islands",
        ["GU"] = "Guam",
        ["AS"] = "American Samoa",
        ["MP"] = "Northern Mariana Islands",
    };

    /// <summary>
    /// True if <paramref name="input"/> is a recognized 2-letter USPS code
    /// (case insensitive). Use from the option validator to reject unknown
    /// 2-letter values before they reach Synthea.
    /// </summary>
    public static bool IsKnownCode(string input)
        => input.Length == 2 && CodeToName.ContainsKey(input.ToUpperInvariant());

    /// <summary>
    /// Converts user-supplied state input to the form Synthea expects.
    /// 2-letter codes → full name (uppercased for lookup). Anything else
    /// (full names, multi-word names) is returned unchanged so Synthea
    /// can validate it. Returns null for null/empty input.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        return input.Length == 2 && CodeToName.TryGetValue(input.ToUpperInvariant(), out var full)
            ? full
            : input;
    }
}
