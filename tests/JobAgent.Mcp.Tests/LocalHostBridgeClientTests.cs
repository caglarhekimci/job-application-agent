using JobAgent.Mcp;
using ModelContextProtocol;

namespace JobAgent.Mcp.Tests;

public sealed class LocalHostBridgeClientTests
{
    [Fact]
    public async Task MissingRegistrationCannotCreateAWorkflowOrAcceptRemoteInputs()
    {
        var path = Path.Combine(Path.GetTempPath(), "jobagent-bridge-missing-" + Guid.NewGuid());
        var client = new LocalHostBridgeClient(new RuntimeStore(path));
        var tools = new SyntheticCommandTools(client);
        var error = await Assert.ThrowsAsync<McpProtocolException>(() => tools.CreateDraft(CancellationToken.None));
        Assert.Contains("UiUnavailable", error.Message);
        Assert.False(Directory.Exists(path));
        await Assert.ThrowsAsync<McpProtocolException>(() => tools.ExecuteApproved("https://example.invalid", CancellationToken.None));
        await Assert.ThrowsAsync<McpProtocolException>(() => tools.PrepareReview("../private", CancellationToken.None));
    }

    [Fact]
    public void MutationContractCannotReceiveApprovalOrCredentials()
    {
        var methods = typeof(SyntheticCommandTools).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Equal(3, methods.Length);
        Assert.All(methods, method => Assert.All(method.GetParameters(),
            parameter => Assert.True(parameter.Name == "applicationRef" || parameter.ParameterType == typeof(CancellationToken))));
    }
}
