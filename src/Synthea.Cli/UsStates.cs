using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Full state/territory names Synthea recognizes (case-insensitive set).
    /// Built from <see cref="CodeToName"/> values, so the two stay in sync
    /// without manual maintenance. (C1)
    /// </summary>
    public static readonly IReadOnlySet<string> KnownFullNames =
        new HashSet<string>(CodeToName.Values, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True if <paramref name="input"/> matches a known full state/territory
    /// name (case insensitive). (C1)
    /// </summary>
    public static bool IsKnownFullName(string input) => KnownFullNames.Contains(input);

    /// <summary>
    /// Returns the closest known state name to <paramref name="input"/>, or
    /// null if nothing is close enough. Used to power a "did you mean ...?"
    /// hint for misspellings. Threshold scales with input length so short
    /// names ("Ohio", "Iowa") need a tighter match than long ones
    /// ("Massachusetts"). (C1)
    /// </summary>
    public static string? SuggestClosest(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        // 1-edit per ~4 characters, minimum 1; so "Maine"→"Maime" (1 edit, len 5) hits,
        // and "Atlantis" (8 chars, no close match) stays null.
        var threshold = Math.Max(1, input.Length / 4);
        var (best, dist) = KnownFullNames
            .Select(n => (Name: n, Dist: LevenshteinIgnoreCase(input, n)))
            .OrderBy(t => t.Dist)
            .First();
        return dist <= threshold ? best : null;
    }

    private static int LevenshteinIgnoreCase(string a, string b)
    {
        var lowerA = a.ToLowerInvariant();
        var lowerB = b.ToLowerInvariant();
        var n = lowerA.Length;
        var m = lowerB.Length;
        if (n == 0) return m;
        if (m == 0) return n;

        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (var j = 0; j <= m; j++) prev[j] = j;

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = lowerA[i - 1] == lowerB[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }

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
