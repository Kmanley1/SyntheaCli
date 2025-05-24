using Xunit;

namespace Synthea.Cli.IntegrationTests;

[Trait("Category", "Integration")]
public class ScaffoldingSmokeTest
{
    [Fact]
    public void Project_Wires_Up() => Assert.True(true);
}
