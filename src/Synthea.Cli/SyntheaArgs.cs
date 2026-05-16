namespace Synthea.Cli;

// Everything that maps to flags or positional values Synthea itself
// consumes. BuildArgumentList depends only on this; nothing about
// hosting (java path, output dir, refresh) belongs here.
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
    string[] Passthru);
