using JobAgent.Mcp;
using ModelContextProtocol.Server;

namespace JobAgent.Mcp.Tests;

public sealed class ApprovalBoundaryTests
{
    [Fact]
    public void ModelCannotCreateApproval()
    {
        var methods = typeof(ReadOnlyTools).GetMethods();
        var exposedNames = methods
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
                .Cast<McpServerToolAttribute>().SingleOrDefault()?.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(exposedNames, name => name!.Contains("approval", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(exposedNames, name => name!.Contains("execute", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(3, exposedNames.Length);
    }
}
