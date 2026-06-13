using System.Text.RegularExpressions;

namespace Synthea.Cli;

// Maps a requested population size to a conservative -Xmx hint. The heuristic
// uses Synthea's own community guidance (4g for 10k+, 8g for 100k+) as the
// midpoints. Returns null at small populations so we don't override the JVM
// default for one-off runs; an explicit --java-heap always wins. (C3)
internal static class JavaHeapSizer
{
    private const long OneGb = 1024L * 1024 * 1024;

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

    // Resolve override → clamped suggestion → null. An explicit override is the
    // raw user input (e.g. "4g"); we prefix "-Xmx" if missing and never clamp it.
    public static string? Resolve(string? overrideValue, int? population)
        => Resolve(overrideValue, population, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, out _);

    // availableBytes is injectable for tests. `note` is set when the auto-suggested
    // tier is clamped down to fit available RAM, so a large -p doesn't crash the JVM
    // with "Could not reserve enough space for object heap". (F-003)
    public static string? Resolve(string? overrideValue, int? population, long availableBytes, out string? note)
    {
        note = null;
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            var trimmed = overrideValue.Trim();
            return trimmed.StartsWith("-Xmx") ? trimmed : "-Xmx" + trimmed;
        }

        if (Suggest(population) is not { } suggested) return null;

        // Suggest only ever emits whole-GB "-XmxNg" tiers — strip "-Xmx" and the
        // trailing 'g'. (If Suggest ever returns an 'm' unit this must be revisited.)
        var wantGb = int.Parse(suggested.Substring("-Xmx".Length, suggested.Length - "-Xmx".Length - 1));
        // ~70% of availableBytes (the .NET process GC budget — host RAM, or the
        // cgroup / GCHeapHardLimit in a container), floored to whole GB.
        var capGb = (int)(availableBytes * 7 / 10 / OneGb);

        if (capGb >= wantGb) return suggested;
        if (capGb >= 1)
        {
            note = $"note: clamped JVM heap to -Xmx{capGb}g (~70% of detected RAM); -p {population} would use {suggested}. " +
                   "Pass --java-heap to override.";
            return $"-Xmx{capGb}g";
        }

        // Under ~1.47 GB available: don't force a heap; let the JVM default decide.
        note = $"note: detected RAM is low (~{availableBytes / (1024 * 1024)} MB) — not setting a -Xmx hint for -p {population}. " +
               "Pass --java-heap to force one.";
        return null;
    }

    // Validator delegate body for --java-heap. Returns the error string on
    // bad input, null on accepted shapes. Kept here next to the regex so
    // RunCommand's option factory stays one-liner-clean.
    public static string? ValidateOverride(string value)
        => HeapShape.IsMatch(value)
            ? null
            : $"--java-heap must be a size like '4g' or '1024m' (got '{value}').";
}
