namespace Synthea.Cli;

// Everything that maps to flags or positional values Synthea itself
// consumes. BuildArgumentList depends only on this; nothing about
// hosting (java path, output dir, refresh) belongs here.
//
// New fields go at the end with defaults so existing positional-arg
// callers in tests don't all need updating each phase.
internal record SyntheaArgs(
    string? State,
    string? City,
    string? Gender,
    string? AgeRange,
    DirectoryInfo? ModuleDir,
    string[]? Modules,
    int? Population,
    int? Seed,
    FileInfo? Config,
    string? Zip,
    string? FhirVersion,
    FileInfo? InitialSnapshot,
    FileInfo? UpdatedSnapshot,
    int? DaysForward,
    string[] Formats,
    string[] AdditionalFormats,
    string[] Passthru,
    // Phase 5 (A1+A2+A3): reproducibility flags accept ISO YYYY-MM-DD;
    // BuildArgumentList converts to Synthea's YYYYMMDD form. -E permits
    // EndDate beyond today; -o true enables overflow generation.
    string? ReferenceDate = null,
    string? EndDate = null,
    bool AllowFutureEnd = false,
    int? ClinicianSeed = null,
    int? SinglePersonSeed = null,
    bool Overflow = false,
    // Phase 6 (A6): repeatable --property KEY=VALUE pairs, emitted to
    // Synthea as `--KEY=VALUE`. Lets callers reach any Synthea property
    // we haven't surfaced as a first-class flag.
    string[]? Properties = null);
