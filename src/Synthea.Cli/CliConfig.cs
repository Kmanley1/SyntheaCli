using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Synthea.Cli;

// Persistent settings loaded from ~/.synthea-cli/config.json (A-40).
// Every field is optional; absent values fall through to the next source
// in the precedence chain: CLI flag > env var > config file > default.
internal sealed record CliConfig(
    string? JarPath,
    bool? InsistChecksum,
    string? GitHubToken,
    string? HttpsProxy)
{
    public static readonly CliConfig Empty = new(null, null, null, null);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".synthea-cli",
        "config.json");

    public static CliConfig Load(string? path = null)
    {
        path ??= DefaultPath();
        if (!File.Exists(path)) return Empty;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CliConfig>(json, JsonOpts) ?? Empty;
        }
        catch (JsonException)
        {
            // A malformed config file should not be a hard failure for every
            // run — fall back to defaults and let the user fix it on their
            // own time. Surfaced to the user via the logger by the caller.
            return Empty;
        }
    }

    // Apply CLI > env > config > default precedence for a string value.
    // The env-getter overload exists so tests can supply an in-memory map
    // instead of mutating process-wide environment state, which would race
    // against parallel tests under the post-A-12 test parallelization.
    public static string? Resolve(string? cliValue, string envVar, string? configValue, string? @default = null)
        => Resolve(cliValue, envVar, configValue, Environment.GetEnvironmentVariable, @default);

    public static string? Resolve(string? cliValue, string envVar, string? configValue,
        Func<string, string?> envGetter, string? @default = null)
    {
        if (!string.IsNullOrEmpty(cliValue)) return cliValue;
        var envValue = envGetter(envVar);
        if (!string.IsNullOrEmpty(envValue)) return envValue;
        if (!string.IsNullOrEmpty(configValue)) return configValue;
        return @default;
    }

    // bool variant: the CLI flag is the only "true on its own"; env var
    // checks for any of "1", "true", "yes" (case-insensitive); config file
    // value wins if all earlier sources are absent.
    public static bool ResolveBool(bool cliFlag, string envVar, bool? configValue, bool @default = false)
        => ResolveBool(cliFlag, envVar, configValue, Environment.GetEnvironmentVariable, @default);

    public static bool ResolveBool(bool cliFlag, string envVar, bool? configValue,
        Func<string, string?> envGetter, bool @default = false)
    {
        if (cliFlag) return true;
        var env = envGetter(envVar);
        if (!string.IsNullOrEmpty(env))
        {
            if (env.Equals("1", StringComparison.Ordinal)) return true;
            if (env.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (env.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (env.Equals("0", StringComparison.Ordinal)) return false;
            if (env.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            if (env.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
        }
        if (configValue.HasValue) return configValue.Value;
        return @default;
    }
}
