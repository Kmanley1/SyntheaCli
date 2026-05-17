using System;

namespace Synthea.Cli;

// Maps known Synthea-JAR error substrings to a one-line remediation hint
// we can print after the underlying process exits non-zero. Keeps the
// remediation knowledge out of RunCommand so adding a new pattern is one
// table entry, not a control-flow change. (C6)
internal static class SyntheaErrorPatterns
{
    // First match wins. Order from most-specific to most-generic.
    private static readonly (string Substring, string Remediation)[] Patterns = new[]
    {
        ("Unable to select a random city id",
         "Synthea couldn't find geography data for the requested state. Use a US state name (e.g. 'Ohio') or the synthea-cli 2-letter code (e.g. 'OH')."),
        ("UnsupportedClassVersionError",
         "Your Java is older than Synthea requires. Install OpenJDK 17 or later, or run `synthea doctor` to inspect your environment."),
        ("OutOfMemoryError",
         "Synthea ran out of heap. Set JAVA_OPTS=-Xmx4g (or higher) in your environment, then re-run. A first-class --java-heap flag is on the v0.5 roadmap."),
        ("Could not allocate",
         "Synthea ran out of heap. Set JAVA_OPTS=-Xmx4g (or higher) in your environment, then re-run."),
        ("Could not find or load main class",
         "JAR appears corrupted or unsupported. Try `synthea cache clear --yes` then re-run to re-download."),
        ("NoClassDefFoundError",
         "JAR appears corrupted or missing dependencies. Try `synthea cache clear --yes` then re-run to re-download."),
        ("FileNotFoundException",
         "Synthea couldn't open a required input file. Check paths passed to --module, --module-dir, --config, or --initial-snapshot."),
    };

    public static string? TryGetRemediation(string? stderrText)
    {
        if (string.IsNullOrEmpty(stderrText)) return null;
        foreach (var (substring, remediation) in Patterns)
        {
            if (stderrText.Contains(substring, StringComparison.OrdinalIgnoreCase))
                return remediation;
        }
        return null;
    }
}
