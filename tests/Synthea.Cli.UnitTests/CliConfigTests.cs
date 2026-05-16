using System;
using System.IO;
using Synthea.Cli;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class CliConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _saved_jarPath;
    private readonly string? _saved_token;
    private readonly string? _saved_proxy;
    private readonly string? _saved_insist;

    public CliConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        // Snapshot env vars so each test runs in a known state and we don't
        // leak across the parallelized suite.
        _saved_jarPath = Environment.GetEnvironmentVariable("SYNTHEA_CLI_JAR_PATH") ?? "";
        _saved_token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        _saved_proxy = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        _saved_insist = Environment.GetEnvironmentVariable("SYNTHEA_CLI_INSIST_CHECKSUM");
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_JAR_PATH", null);
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        Environment.SetEnvironmentVariable("HTTPS_PROXY", null);
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_INSIST_CHECKSUM", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_JAR_PATH",
            string.IsNullOrEmpty(_saved_jarPath) ? null : _saved_jarPath);
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", _saved_token);
        Environment.SetEnvironmentVariable("HTTPS_PROXY", _saved_proxy);
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_INSIST_CHECKSUM", _saved_insist);
        try { Directory.Delete(_tempDir, true); } catch { }
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
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_JAR_PATH", "/env/path");
        var resolved = CliConfig.Resolve("/cli/path", "SYNTHEA_CLI_JAR_PATH", "/config/path");
        Assert.Equal("/cli/path", resolved);
    }

    [Fact]
    public void Resolve_EnvWinsOverConfig()
    {
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_JAR_PATH", "/env/path");
        var resolved = CliConfig.Resolve(null, "SYNTHEA_CLI_JAR_PATH", "/config/path");
        Assert.Equal("/env/path", resolved);
    }

    [Fact]
    public void Resolve_ConfigWinsOverDefault()
    {
        var resolved = CliConfig.Resolve(null, "SYNTHEA_CLI_JAR_PATH", "/config/path", "/default");
        Assert.Equal("/config/path", resolved);
    }

    [Fact]
    public void Resolve_FallsThroughToDefault()
    {
        var resolved = CliConfig.Resolve(null, "SYNTHEA_CLI_JAR_PATH", null, "/default");
        Assert.Equal("/default", resolved);
    }

    [Fact]
    public void ResolveBool_CliFlagWins()
    {
        Assert.True(CliConfig.ResolveBool(cliFlag: true, "SYNTHEA_CLI_INSIST_CHECKSUM", configValue: false));
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
        Environment.SetEnvironmentVariable("SYNTHEA_CLI_INSIST_CHECKSUM", envValue);
        Assert.Equal(expected, CliConfig.ResolveBool(cliFlag: false, "SYNTHEA_CLI_INSIST_CHECKSUM", configValue: null));
    }

    [Fact]
    public void ResolveBool_ConfigWinsOverDefault()
    {
        Assert.True(CliConfig.ResolveBool(cliFlag: false, "SYNTHEA_CLI_INSIST_CHECKSUM", configValue: true));
    }

    [Fact]
    public void BuildHttpClient_WiresProxyFromEnvVar()
    {
        Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://envproxy:8080");
        var client = Program.BuildHttpClient(CliConfig.Empty);
        Assert.NotNull(client);
        // We can't pull the handler off an HttpClient, but BuildHttpClient
        // sets the User-Agent header unconditionally, so the smoke check is
        // that it's there and the call didn't throw building the proxy.
        Assert.Contains(client.DefaultRequestHeaders.UserAgent, p => p.Product?.Name == "Synthea.Cli");
    }

    [Fact]
    public void ResolveJarOverrides_CompositesCliEnvAndConfig()
    {
        // CLI sets InsistChecksum and JarPath; env supplies token. Together
        // with config they should produce a JarOverrides populated by the
        // precedence rules. Proxy is wired into the HttpClient at construction
        // time (see Program.BuildHttpClient) so it does not appear here.
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "envtok");

        var hosting = new HostingOptions(
            Output: new DirectoryInfo(_tempDir),
            Refresh: false,
            JavaPath: "java",
            JarPath: "/cli/jar",
            InsistChecksum: true);
        var config = new CliConfig(JarPath: "/cfg/jar", InsistChecksum: false, GitHubToken: "cfgtok", HttpsProxy: "http://cfg");

        var overrides = RunCommand.ResolveJarOverrides(hosting, config);
        Assert.Equal("/cli/jar", overrides.JarPath);    // CLI > config
        Assert.True(overrides.InsistChecksum);          // CLI flag wins
        Assert.Equal("envtok", overrides.GitHubToken);  // env > config
    }
}
