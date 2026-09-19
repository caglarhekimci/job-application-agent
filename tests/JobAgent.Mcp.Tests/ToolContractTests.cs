using System.Reflection;

namespace JobAgent.Mcp.Tests;

public sealed class ToolContractTests
{
    [Fact]
    public void ReadOnlyToolContractExists()
    {
        var assembly = Assembly.Load("JobAgent.Mcp");

        var tools = assembly.GetType("JobAgent.Mcp.ReadOnlyTools");

        Assert.NotNull(tools);
        Assert.NotNull(tools.GetMethod("RuntimeGetCapabilities"));
        Assert.NotNull(tools.GetMethod("ProfileGetSummary"));
        Assert.NotNull(tools.GetMethod("ApplicationGetStatus"));
    }
}
