using JobAgent.Infrastructure.Bridge;
using JobAgent.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace JobAgent.E2E.Tests;

public sealed class ModelPolicyBridgeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-model-policy-bridge-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Api", false, 4, false)]
    [InlineData("HostMediated", true, 4, false)]
    [InlineData("HostMediated", false, 0, false)]
    [InlineData("HostMediated", false, 11, false)]
    [InlineData("HostMediated", false, 4, true)]
    public async Task ReadOnlyCapabilityRejectsUnsafeOrExpandedPolicyClaims(string mode, bool paid,
        int maximum, bool addUnknownClaim)
    {
        var unknown = addUnknownClaim ? ",\"hostQuotaControlled\":true" : "";
        var body = $$"""
            {"revision":0,"profileRef":null,"profileVersion":null,"resumeRef":null,"jobRef":null,"providerMode":"{{mode}}","paidApiEnabled":{{paid.ToString().ToLowerInvariant()}},"maxAnswerProposalOperationsPerApplication":{{maximum}}{{unknown}}}
            """;
        await using var app = BuildHost(body);
        app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
        await HostBridgeRegistrationStore.WriteAsync(root, new(new Uri(app.Urls.Single()),
            new string('B', 64), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5)));
        var client = new WorkspaceHostBridgeClient(new RuntimeStore(root, enableLocalCommands: true));

        var error = await Assert.ThrowsAsync<McpProtocolException>(() => client.GetHostWorkspaceRefsAsync());

        Assert.Contains("InvalidBridgeResponse", error.Message, StringComparison.Ordinal);
    }

    private static WebApplication BuildHost(string body)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapPost("/{**path}", async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        });
        return app;
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
