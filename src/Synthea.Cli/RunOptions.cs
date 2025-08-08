namespace Synthea.Cli;

internal record RunOptions(
    DirectoryInfo Output,
    bool Refresh,
    string JavaPath,
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
    string[] Passthru);
