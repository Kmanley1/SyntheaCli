using System;
using System.Threading.Tasks;
using Xunit;

namespace Synthea.Cli.UnitTests;

public class CliTests
{
    [Fact]
    public async Task DefaultsToHelpWhenNoArgs()
    {
        var exit = await Program.Main(Array.Empty<string>());
        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task RunCommandAllowsUnknownOptions()
    {
        var exit = await Program.Main(new[] { "run", "--help", "--unknown", "value" });
        Assert.Equal(0, exit);
    }
}
