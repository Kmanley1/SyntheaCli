using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Synthea.Cli.Tests;

public class CliTests
{
    private static Task<int> InvokeMain(params string[] args)
    {
        var program = System.Reflection.Assembly.Load("Synthea.Cli").GetType("Synthea.Cli.Program")
            ?? throw new System.InvalidOperationException();
        var method = program.GetMethod("Main", BindingFlags.Static | BindingFlags.Public)!;
        return (Task<int>)method.Invoke(null, new object[] { args })!;
    }

    [Fact]
    public async Task DefaultsToHelpWhenNoArgs()
    {
        var exit = await InvokeMain(System.Array.Empty<string>());
        Assert.Equal(0, exit);
    }

    [Fact]
    public async Task RunCommandAllowsUnknownOptions()
    {
        var exit = await InvokeMain("run", "--help", "--unknown", "value");
        Assert.Equal(0, exit);
    }
}

