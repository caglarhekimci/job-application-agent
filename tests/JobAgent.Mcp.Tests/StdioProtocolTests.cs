using System.Collections.Concurrent;
using System.Text.Json;
using JobAgent.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace JobAgent.Mcp.Tests;

public sealed class StdioProtocolTests
{
    [Fact]
    public async Task StdoutContainsOnlyProtocol_AndActualStdioHandshakeListAndCallsWork()
    {
        await using var fixture = new RuntimeFixture();
        await fixture.SeedAsync();
        var stderr = new ConcurrentQueue<string>();
        await using var client = await CreateClientAsync(fixture.DirectoryPath, stderr);

        var tools = await client.ListToolsAsync();
        var capabilities = await client.CallToolAsync("runtime_get_capabilities");
        var profile = await client.CallToolAsync("profile_get_summary", new Dictionary<string, object?>
        {
            ["profileRef"] = fixture.Profile.Id.ToString("D")
        });
        var application = await client.CallToolAsync("application_get_status", new Dictionary<string, object?>
        {
            ["applicationRef"] = fixture.Application.Draft.Id.ToString("D")
        });

        Assert.Equal(
            ["application_get_status", "profile_get_summary", "runtime_get_capabilities"],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
        Assert.All(tools, tool => Assert.False(tool.ProtocolTool.Annotations?.OpenWorldHint));
        Assert.NotEqual(true, capabilities.IsError);
        Assert.Contains("NotVerifiedOnHost", ResultJson(capabilities), StringComparison.Ordinal);
        Assert.Contains("C#", ResultJson(profile), StringComparison.Ordinal);
        Assert.DoesNotContain("85000", ResultJson(profile), StringComparison.Ordinal);
        Assert.Contains("SubmittedVerified", ResultJson(application), StringComparison.Ordinal);
        Assert.DoesNotContain(stderr, line => line.Contains("candidate@example.invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ToolSchema_RejectsUnknownFields()
    {
        await using var fixture = new RuntimeFixture();
        await fixture.SeedAsync();
        await using var client = await CreateClientAsync(fixture.DirectoryPath, new ConcurrentQueue<string>());

        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await client.CallToolAsync("profile_get_summary", new Dictionary<string, object?>
            {
                ["profileRef"] = fixture.Profile.Id.ToString("D"),
                ["path"] = fixture.ProfileDatabasePath
            });
            Assert.True(result.IsError);
        });

        Assert.Null(exception);
    }

    private static async Task<McpClient> CreateClientAsync(string runtimeDirectory, ConcurrentQueue<string> stderr)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? throw new InvalidOperationException("DOTNET_ROOT is required for the pinned SDK test.");
        var dotnet = Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        var serverDll = typeof(ReadOnlyTools).Assembly.Location;
        var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        environment[RuntimeStore.RuntimeDirectoryEnvironmentVariable] = runtimeDirectory;
        environment["DOTNET_ROOT"] = dotnetRoot;
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        var transport = new StdioClientTransport(new()
        {
            Name = "job-agent-mcp-test",
            Command = dotnet,
            Arguments = [serverDll],
            WorkingDirectory = Environment.CurrentDirectory,
            InheritEnvironmentVariables = false,
            EnvironmentVariables = environment,
            StandardErrorLines = line => stderr.Enqueue(line),
            ShutdownTimeout = TimeSpan.FromSeconds(2)
        });
        return await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = "2025-11-25" });
    }

    private static string ResultJson(CallToolResult result) =>
        result.StructuredContent?.GetRawText() ?? JsonSerializer.Serialize(result.Content);
}
