using System.Text.Json;
using JobAgent.Core.Applications;
using JobAgent.Infrastructure.Bridge;
using JobAgent.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace JobAgent.E2E.Tests;

public sealed class HostBridgeResponseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-bridge-response-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(ApplicationStatus.SubmittedVerified, false, null)]
    [InlineData(ApplicationStatus.SubmittedVerified, false, "")]
    [InlineData(ApplicationStatus.SubmittedVerified, false, " ")]
    [InlineData(ApplicationStatus.SubmittedUnverified, false, "SYN-unexpected")]
    [InlineData(ApplicationStatus.ReadyForDataSharing, true, null)]
    public async Task ImpossibleSuccessSummary_IsRejected(
        ApplicationStatus status, bool submissionApproved, string? receiptId)
    {
        var applicationRef = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            applicationRef,
            status = status.ToString(),
            reviewRequested = true,
            submissionApproved,
            receiptId
        });

        var error = await SendExecuteAsync(applicationRef, StatusCodes.Status200OK, body);

        Assert.Contains("InvalidBridgeResponse", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StatusCodes.Status500InternalServerError, "{}", "CommandOutcomeUnknownCheckStatus")]
    [InlineData(StatusCodes.Status409Conflict, "{\"error\":\"ConsentRequired\"}", "ConsentRequired")]
    [InlineData(StatusCodes.Status409Conflict, "{\"error\":\"ConcurrentChange\"}", "CommandOutcomeUnknownCheckStatus")]
    public async Task ExecuteError_IsClassifiedByWhetherPreclaimRejectionIsCertain(
        int statusCode, string body, string expectedCode)
    {
        var error = await SendExecuteAsync(Guid.NewGuid(), statusCode, body);

        Assert.Contains(expectedCode, error.Message, StringComparison.Ordinal);
    }

    private async Task<McpProtocolException> SendExecuteAsync(Guid applicationRef, int statusCode, string body)
    {
        await using var host = BuildHost(statusCode, body);
        await host.StartAsync();
        await HostBridgeRegistrationStore.WriteAsync(root, new(
            new Uri(host.Urls.Single()), new string('B', 64), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5)));
        var client = new LocalHostBridgeClient(new RuntimeStore(root, enableSyntheticCommands: true));
        return await Assert.ThrowsAsync<McpProtocolException>(() => client.SendAsync(
            HostBridgeOperation.ExecuteApproved, applicationRef, CancellationToken.None));
    }

    private static WebApplication BuildHost(int statusCode, string body)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapPost("/{**path}", async context =>
        {
            context.Response.StatusCode = statusCode;
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
