using System.Text.RegularExpressions;

namespace Synthea.Cli;

// Maps a requested population size to a conservative -Xmx hint. The heuristic
// uses Synthea's own community guidance (4g for 10k+, 8g for 100k+) as the
// midpoints. Returns null at small populations so we don't override the JVM
// default for one-off runs; an explicit --java-heap always wins. (C3)
internal static class JavaHeapSizer
{
    // <digits> + g/G/m/M. Trailing whitespace allowed for forgiving CLI input.
    internal static readonly Regex HeapShape =
        new("^\\s*(?<n>\\d+)(?<unit>[gGmM])\\s*$", RegexOptions.Compiled);

    public static string? Suggest(int? population)
    {
        if (!population.HasValue || population.Value < 1000) return null;
        if (population.Value < 10000) return "-Xmx2g";
        if (population.Value < 100000) return "-Xmx4g";
        if (population.Value < 1000000) return "-Xmx8g";
        return "-Xmx16g";
    }

    // Resolve override → suggestion → null. The override is the raw user
    // input (e.g. "4g"); we prefix "-Xmx" if missing so callers can pass
    // either form.
    public static string? Resolve(string? overrideValue, int? population)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            var trimmed = overrideValue.Trim();
            return trimmed.StartsWith("-Xmx") ? trimmed : "-Xmx" + trimmed;
        }
        return Suggest(population);
    }

    // Validator delegate body for --java-heap. Returns the error string on
    // bad input, null on accepted shapes. Kept here next to the regex so
    // RunCommand's option factory stays one-liner-clean.
    public static string? ValidateOverride(string value)
        => HeapShape.IsMatch(value)
            ? null
            : $"--java-heap must be a size like '4g' or '1024m' (got '{value}').";
}
