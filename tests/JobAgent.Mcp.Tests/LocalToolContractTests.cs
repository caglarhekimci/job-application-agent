using JobAgent.Mcp;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace JobAgent.Mcp.Tests;

public sealed class LocalToolContractTests
{
    private static readonly string[] ExpectedLocalTools =
    [
        "runtime_get_capabilities", "profile_get_summary", "profile_propose_patch", "job_import_text",
        "job_evaluate", "application_create_draft", "application_get_questions", "application_propose_answers",
        "application_prepare_review", "application_execute_approved", "application_get_status", "application_cancel"
    ];

    [Fact]
    public async Task ExplicitLocalModeHasTwelveNarrowToolsAndNoApprovalSurface()
    {
        await using var client = await StartAsync(local: true);
        var tools = await client.ListToolsAsync();
        Assert.Equal(ExpectedLocalTools.Order(), tools.Select(t => t.Name).Order());
        Assert.All(tools, t => Assert.False(t.ProtocolTool.Annotations!.OpenWorldHint));
        Assert.True(Assert.Single(tools, t => t.Name == "application_execute_approved").ProtocolTool.Annotations!.DestructiveHint);
        foreach (var tool in tools)
        {
            var schema = tool.ProtocolTool.InputSchema.GetRawText();
            Assert.DoesNotContain("approved", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sessionId", schema, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("filePath", schema, StringComparison.OrdinalIgnoreCase);
        }
        var bad = await client.CallToolAsync("application_create_draft", new Dictionary<string, object?>
        {
            ["jobRef"] = Guid.NewGuid().ToString("D"),
            ["profileRef"] = Guid.NewGuid().ToString("D"),
            ["resumeRef"] = Guid.NewGuid().ToString("D"),
            ["approved"] = true
        });
        Assert.True(bad.IsError);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 6)]
    public async Task DefaultAndLegacyCatalogsRemainUnchanged(bool synthetic, int count)
    {
        await using var client = await StartAsync(local: false, synthetic);
        Assert.Equal(count, (await client.ListToolsAsync()).Count);
    }

    [Theory]
    [InlineData("nested-unknown")]
    [InlineData("nested-duplicate")]
    [InlineData("salary-field")]
    [InlineData("oversized")]
    [InlineData("invalid-reference")]
    public async Task HostRejectsMalformedProposalBeforeAccessingCompanion(string mutation)
    {
        await using var client = await StartAsync(local: true);
        var profileRef = mutation == "invalid-reference" ? "../private.db" : Guid.NewGuid().ToString("D");
        var change = mutation switch
        {
            "nested-unknown" => "{\"field\":\"FullName\",\"value\":\"name\",\"evidenceRefs\":[],\"verifiedAt\":\"now\"}",
            "nested-duplicate" => "{\"field\":\"FullName\",\"value\":\"first\",\"value\":\"second\",\"evidenceRefs\":[]}",
            "salary-field" => "{\"field\":\"SalaryPrivateMinimum\",\"value\":\"1\",\"evidenceRefs\":[]}",
            _ => JsonSerializer.Serialize(new { field = "FullName", value = mutation == "oversized" ? new string('x', 300000) : "name", evidenceRefs = Array.Empty<string>() })
        };
        using var changes = JsonDocument.Parse("[" + change + "]");
        var result = await client.CallToolAsync("profile_propose_patch", new Dictionary<string, object?>
        { ["profileRef"] = profileRef, ["baseVersion"] = 1, ["changes"] = changes.RootElement.Clone() });
        Assert.True(result.IsError);
        Assert.Contains("Invalid local tool arguments", JsonSerializer.Serialize(result.Content));
    }

    private static async Task<McpClient> StartAsync(bool local, bool synthetic = false)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")!;
        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environment["DOTNET_ROOT"] = dotnetRoot;
        environment["JOBAGENT_RUNTIME_DIR"] = Path.Combine(Path.GetTempPath(), "jobagent-local-contract-missing-" + Guid.NewGuid());
        environment["JOBAGENT_ENABLE_LOCAL_COMMANDS"] = local ? "1" : "0";
        environment["JOBAGENT_ENABLE_SYNTHETIC_COMMANDS"] = synthetic ? "1" : "0";
        return await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "local-contract-test",
            Command = Path.Combine(dotnetRoot, "dotnet.exe"),
            Arguments = [typeof(ReadOnlyTools).Assembly.Location],
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
            ShutdownTimeout = TimeSpan.FromSeconds(2)
        }));
    }
}
