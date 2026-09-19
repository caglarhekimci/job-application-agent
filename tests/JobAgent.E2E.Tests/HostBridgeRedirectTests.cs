using JobAgent.Infrastructure.Bridge;
using JobAgent.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace JobAgent.E2E.Tests;

public sealed class HostBridgeRedirectTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-bridge-redirect-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ClientRejectsRedirectWithoutFollowingOrForwardingBridgeToken()
    {
        var token = new string('A', 64);
        var sinkRequests = 0;
        string? sinkToken = null;
        await using var sink = BuildHost(app => app.MapPost("/{**path}", (HttpContext context) =>
        {
            Interlocked.Increment(ref sinkRequests);
            sinkToken = context.Request.Headers["X-JobAgent-Bridge"].ToString();
            return Results.Ok();
        }));
        await sink.StartAsync();

        var redirectRequests = 0;
        string? redirectToken = null;
        await using var redirector = BuildHost(app => app.MapPost("/internal/mcp/applications", (HttpContext context) =>
        {
            Interlocked.Increment(ref redirectRequests);
            redirectToken = context.Request.Headers["X-JobAgent-Bridge"].ToString();
            context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
            context.Response.Headers.Location = new Uri(new Uri(sink.Urls.Single()), "capture").AbsoluteUri;
            return Task.CompletedTask;
        }));
        await redirector.StartAsync();

        Directory.CreateDirectory(root);
        await HostBridgeRegistrationStore.WriteAsync(root, new(
            new Uri(redirector.Urls.Single()), token, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5)));
        var client = new LocalHostBridgeClient(new RuntimeStore(root, enableSyntheticCommands: true));

        var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
            client.SendAsync(HostBridgeOperation.CreateDraft, null, CancellationToken.None));

        Assert.Contains("BridgeRedirectBlocked", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, redirectRequests);
        Assert.Equal(token, redirectToken);
        Assert.Equal(0, sinkRequests);
        Assert.Null(sinkToken);
    }

    private static WebApplication BuildHost(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        map(app);
        return app;
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
