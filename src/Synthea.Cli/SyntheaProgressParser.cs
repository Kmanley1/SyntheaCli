using System.Text.RegularExpressions;

namespace Synthea.Cli;

// D2: Synthea reports per-patient generation progress as stderr lines
// shaped like `N -- Name (age y/o gender) City, State`. We don't try
// to parse the name/age/location — just the leading integer, which is
// Synthea's running count of generated patients. The CLI samples the
// most recent value on a timer and renders a "Generated X/Y (Z%)..."
// status line so users can see big runs making forward progress
// without having to grep stderr themselves.
internal sealed class SyntheaProgressParser
{
    // The leading integer is the only signal we need. Synthea pads with
    // a leading space when the count crosses 10/100/1000, hence \s* on
    // each side. We anchor with ^ so we don't match the same integer
    // when it shows up inside an exception message later.
    private static readonly Regex LineRegex = new(
        @"^\s*(?<count>\d+)\s+--\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private int _lastCount;

    internal int LastCount => _lastCount;

    // Returns true if the line was a progress line (and updated state),
    // false otherwise. Lines that look like progress but report a count
    // lower than what we've already seen are ignored — Synthea threads
    // can emit out of order, and we only want monotonic forward motion.
    internal bool TryConsume(string line)
    {
        if (string.IsNullOrEmpty(line)) return false;
        var m = LineRegex.Match(line);
        if (!m.Success) return false;
        if (!int.TryParse(m.Groups["count"].Value, out var n)) return false;
        if (n <= _lastCount) return true;  // recognized but stale; still a progress line
        _lastCount = n;
        return true;
    }
}
