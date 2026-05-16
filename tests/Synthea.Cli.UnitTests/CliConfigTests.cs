using System;
using System.Collections.Generic;
using System.IO;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

// Tests use in-memory env dictionaries (passed via the envGetter overload)
// instead of mutating Environment.SetEnvironmentVariable. Process-wide env
// state would race with parallel tests under Phase 5's parallelization.
public class CliConfigTests : IDisposable
{
    private readonly string _tempDir;

    public CliConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private static Func<string, string?> Env(params (string Key, string Value)[] entries)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (k, v) in entries) map[k] = v;
        return key => map.TryGetValue(key, out var v) ? v : null;
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "missing.json");
        var c = CliConfig.Load(path);
        Assert.Same(CliConfig.Empty, c);
    }

    [Fact]
    public void Load_ValidFile_ParsesAllFields()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, """
            {
              "jarPath": "/opt/synthea.jar",
              "insistChecksum": true,
              "githubToken": "ghp_abc",
              "httpsProxy": "http://proxy:8080"
            }
            """);
        var c = CliConfig.Load(path);
        Assert.Equal("/opt/synthea.jar", c.JarPath);
        Assert.True(c.InsistChecksum);
        Assert.Equal("ghp_abc", c.GitHubToken);
        Assert.Equal("http://proxy:8080", c.HttpsProxy);
    }

    [Fact]
    public void Load_MalformedFile_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "broken.json");
        File.WriteAllText(path, "{not valid json");
        var c = CliConfig.Load(path);
        Assert.Same(CliConfig.Empty, c);
    }

    [Fact]
    public void Resolve_CliWinsOverEnvAndConfig()
    {
        var env = Env(("SYNTHEA_CLI_JAR_PATH", "/env/path"));
        var resolved = CliConfig.Resolve("/cli/path", "SYNTHEA_CLI_JAR_PATH", "/config/path", env);
        Assert.Equal("/cli/path", resolved);
    }

    [Fact]
    public void Resolve_EnvWinsOverConfig()
    {
        var env = Env(("SYNTHEA_CLI_JAR_PATH", "/env/path"));
        var resolved = CliConfig.Resolve(null, "SYNTHEA_CLI_JAR_PATH", "/config/path", env);
        Assert.Equal("/env/path", resolved);
    }

    [Fact]
    public void Resolve_ConfigWinsOverDefault()
    {
        var resolved = CliConfig.Resolve(null, "SYNTHEA_CLI_JAR_PATH", "/config/path", Env(), "/default");
        Assert.Equal("/config/path", resolved);
    }

    [Fact]
    public void Resolve_FallsThroughToDefault()
    {
        var resolved = CliConfig.Resolve(null, "SYNTHEA_CLI_JAR_PATH", null, Env(), "/default");
        Assert.Equal("/default", resolved);
    }

    [Fact]
    public void ResolveBool_CliFlagWins()
    {
        Assert.True(CliConfig.ResolveBool(cliFlag: true, "SYNTHEA_CLI_INSIST_CHECKSUM", configValue: false, Env()));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    public void ResolveBool_EnvVarParsesCommonShapes(string envValue, bool expected)
    {
        var env = Env(("SYNTHEA_CLI_INSIST_CHECKSUM", envValue));
        Assert.Equal(expected, CliConfig.ResolveBool(cliFlag: false, "SYNTHEA_CLI_INSIST_CHECKSUM", configValue: null, env));
    }

    [Fact]
    public void ResolveBool_ConfigWinsOverDefault()
    {
        Assert.True(CliConfig.ResolveBool(cliFlag: false, "SYNTHEA_CLI_INSIST_CHECKSUM", configValue: true, Env()));
    }

    [Fact]
    public void ResolveJarOverrides_CompositesCliEnvAndConfig()
    {
        // CLI sets InsistChecksum and JarPath; env supplies token. Together
        // with config they should produce a JarOverrides populated by the
        // precedence rules. Proxy is wired into the HttpClient at construction
        // time (see Program.BuildHttpClient) so it does not appear here.
        var env = Env(("GITHUB_TOKEN", "envtok"));

        var hosting = new HostingOptions(
            Output: new DirectoryInfo(_tempDir),
            Refresh: false,
            JavaPath: "java",
            JarPath: "/cli/jar",
            InsistChecksum: true);
        var config = new CliConfig(JarPath: "/cfg/jar", InsistChecksum: false, GitHubToken: "cfgtok", HttpsProxy: "http://cfg");

        var overrides = RunCommand.ResolveJarOverrides(hosting, config, env);
        Assert.Equal("/cli/jar", overrides.JarPath);    // CLI > config
        Assert.True(overrides.InsistChecksum);          // CLI flag wins
        Assert.Equal("envtok", overrides.GitHubToken);  // env > config
    }
}
