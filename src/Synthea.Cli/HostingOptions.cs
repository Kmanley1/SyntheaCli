namespace Synthea.Cli;

// Controls how the CLI hosts the Synthea run: where output lands, whether
// to refresh the cached JAR, which Java executable to invoke, and the four
// JarManager config inputs (A-5/A-36/A-40). Kept separate from SyntheaArgs
// so DI/composition layers can resolve hosting concerns without dragging
// the domain argument surface along.
internal record HostingOptions(
    DirectoryInfo Output,
    bool Refresh,
    string JavaPath,
    string? JarPath = null,
    bool InsistChecksum = false);
