using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Synthea.Cli;

// Module name + where it came from. Location is the zip entry path inside
// the JAR ("modules/medications/blood_pressure.json") for JAR sources, or
// the absolute filesystem path for --module-dir sources.
internal sealed record ModuleEntry(string Name, string Location, ModuleSource Source);

internal enum ModuleSource { Jar, Directory }

// Subset of a Synthea GMF JSON we surface in `synthea modules describe`.
// StateCount is the count of keys under the top-level "states" object,
// which is what Synthea's documentation calls a state machine state.
internal sealed record ModuleDescription(
    string Name,
    string? Remarks,
    string? GmfVersion,
    int StateCount,
    string Location);

// Stateless helpers for zip-walking a Synthea JAR. A small on-disk cache
// keyed by the JAR's SHA-256 prefix avoids re-zipping ~200 entries on
// every `synthea modules list` invocation. Callers in production wire
// the cache directory; tests pass null to bypass caching entirely.
// (B4)
internal static class ModuleIntrospector
{
    internal const string JarModulesPrefix = "modules/";

    // ---- Public surface ------------------------------------------------

    public static IReadOnlyList<ModuleEntry> ListJarModules(string jarPath, string? cacheDir = null)
    {
        if (string.IsNullOrWhiteSpace(jarPath))
            throw new ArgumentException("JAR path must be provided.", nameof(jarPath));
        if (!File.Exists(jarPath))
            throw new FileNotFoundException($"JAR not found: {jarPath}", jarPath);

        if (cacheDir is not null && TryReadCache(jarPath, cacheDir, out var cached))
            return cached;

        var entries = ZipWalk(jarPath);
        if (cacheDir is not null)
            TryWriteCache(jarPath, cacheDir, entries);
        return entries;
    }

    public static IReadOnlyList<ModuleEntry> ListDirectoryModules(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Module directory path must be provided.", nameof(directoryPath));
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Module directory not found: {directoryPath}");

        var root = new DirectoryInfo(directoryPath);
        var prefix = root.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return root.GetFiles("*.json", SearchOption.AllDirectories)
            .Select(f =>
            {
                var rel = f.FullName.StartsWith(prefix, StringComparison.Ordinal)
                    ? f.FullName.Substring(prefix.Length)
                    : f.Name;
                return new ModuleEntry(LeafName(rel), f.FullName, ModuleSource.Directory);
            })
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ModuleDescription DescribeJarModule(string jarPath, string nameOrEntryPath)
    {
        if (string.IsNullOrWhiteSpace(jarPath)) throw new ArgumentException("JAR path required", nameof(jarPath));
        if (!File.Exists(jarPath)) throw new FileNotFoundException($"JAR not found: {jarPath}", jarPath);
        if (string.IsNullOrWhiteSpace(nameOrEntryPath))
            throw new ArgumentException("Module name required.", nameof(nameOrEntryPath));

        using var archive = ZipFile.OpenRead(jarPath);
        var entry = ResolveJarEntry(archive, nameOrEntryPath);
        using var stream = entry.Open();
        using var doc = JsonDocument.Parse(stream);
        return BuildDescription(entry.FullName, doc, ModuleSource.Jar);
    }

    public static ModuleDescription DescribeFileModule(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Module file not found: {filePath}", filePath);
        using var stream = File.OpenRead(filePath);
        using var doc = JsonDocument.Parse(stream);
        return BuildDescription(filePath, doc, ModuleSource.Directory);
    }

    // Exposed so the command layer can compute the SHA prefix for "modules-cache-<sha8>.json"
    // before deciding whether to honor a cache hit. Internal to keep tests honest.
    internal static string ComputeJarShaPrefix(string jarPath)
    {
        using var fs = File.OpenRead(jarPath);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash).Substring(0, 8).ToLowerInvariant();
    }

    // ---- Internals -----------------------------------------------------

    private static IReadOnlyList<ModuleEntry> ZipWalk(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var results = new List<ModuleEntry>();
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.StartsWith(JarModulesPrefix, StringComparison.Ordinal)) continue;
            if (!entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.Length == 0) continue;
            var name = LeafName(entry.FullName.Substring(JarModulesPrefix.Length));
            results.Add(new ModuleEntry(name, entry.FullName, ModuleSource.Jar));
        }
        return results.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ZipArchiveEntry ResolveJarEntry(ZipArchive archive, string nameOrEntryPath)
    {
        var direct = nameOrEntryPath.StartsWith(JarModulesPrefix, StringComparison.Ordinal)
            ? nameOrEntryPath
            : JarModulesPrefix + nameOrEntryPath + (nameOrEntryPath.EndsWith(".json") ? "" : ".json");

        var entry = archive.GetEntry(direct);
        if (entry is not null) return entry;

        // Leaf-name fallback: search for a unique entry whose *filename*
        // (last path segment, without `.json`) matches the user's input.
        // We match on the bare filename, not the relative path under
        // modules/, so `describe dup` resolves either `a/dup.json` or
        // `b/dup.json` to its full path — and reports ambiguity if both
        // exist.
        var leafTarget = nameOrEntryPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(nameOrEntryPath)
            : nameOrEntryPath;
        var matches = archive.Entries
            .Where(e => e.FullName.StartsWith(JarModulesPrefix, StringComparison.Ordinal)
                     && e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(Path.GetFileNameWithoutExtension(e.FullName), leafTarget, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1) return matches[0];
        if (matches.Count == 0)
            throw new InvalidOperationException($"Module '{nameOrEntryPath}' not found in JAR. Try `synthea modules list` to see available names.");
        var paths = string.Join(", ", matches.Select(m => m.FullName));
        throw new InvalidOperationException($"Module name '{nameOrEntryPath}' is ambiguous: {paths}. Pass the full path instead.");
    }

    private static ModuleDescription BuildDescription(string location, JsonDocument doc, ModuleSource source)
    {
        var root = doc.RootElement;
        string? name = TryGetString(root, "name");
        string? remarks = null;
        if (root.TryGetProperty("remarks", out var remarksEl))
        {
            remarks = remarksEl.ValueKind switch
            {
                JsonValueKind.String => remarksEl.GetString(),
                JsonValueKind.Array => string.Join(' ', remarksEl.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString())),
                _ => null,
            };
        }
        // gmf_version is sometimes a JSON number, sometimes a string —
        // Synthea's own modules vary. Render either as a flat string.
        string? gmfVersion = null;
        if (root.TryGetProperty("gmf_version", out var gmfEl))
        {
            gmfVersion = gmfEl.ValueKind switch
            {
                JsonValueKind.String => gmfEl.GetString(),
                JsonValueKind.Number => gmfEl.GetRawText(),
                _ => null,
            };
        }
        int stateCount = 0;
        if (root.TryGetProperty("states", out var statesEl) && statesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var _ in statesEl.EnumerateObject()) stateCount++;
        }
        // Fall back to the file's leaf name if the JSON omits "name" — some
        // Synthea modules do this.
        var displayName = name ?? LeafName(location);
        return new ModuleDescription(displayName, remarks, gmfVersion, stateCount, location);
    }

    private static string? TryGetString(JsonElement root, string property)
        => root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static string LeafName(string relativePath)
    {
        var withoutExt = relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? relativePath.Substring(0, relativePath.Length - ".json".Length)
            : relativePath;
        // Normalize separators so a jar entry "modules/x/y.json" and a
        // filesystem path "modules\x\y.json" yield the same leaf.
        return withoutExt.Replace('\\', '/');
    }

    // ---- Cache layer ---------------------------------------------------

    private sealed record CacheBlob(string JarSha, ModuleEntry[] Modules);

    private static string CacheFilePath(string cacheDir, string shaPrefix)
        => Path.Combine(cacheDir, $"modules-cache-{shaPrefix}.json");

    private static bool TryReadCache(string jarPath, string cacheDir, out IReadOnlyList<ModuleEntry> entries)
    {
        entries = Array.Empty<ModuleEntry>();
        try
        {
            var sha = ComputeJarShaPrefix(jarPath);
            var path = CacheFilePath(cacheDir, sha);
            if (!File.Exists(path)) return false;
            var json = File.ReadAllText(path);
            var blob = JsonSerializer.Deserialize<CacheBlob>(json);
            if (blob is null || !string.Equals(blob.JarSha, sha, StringComparison.Ordinal)) return false;
            entries = blob.Modules;
            return true;
        }
        catch (IOException) { return false; }
        catch (JsonException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static void TryWriteCache(string jarPath, string cacheDir, IReadOnlyList<ModuleEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(cacheDir);
            var sha = ComputeJarShaPrefix(jarPath);
            var blob = new CacheBlob(sha, entries.ToArray());
            File.WriteAllText(CacheFilePath(cacheDir, sha), JsonSerializer.Serialize(blob));
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}
