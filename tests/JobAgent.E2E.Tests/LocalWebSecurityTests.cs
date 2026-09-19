using System.Net;
using System.Net.Http.Json;
using JobAgent.Web;

namespace JobAgent.E2E.Tests;

public sealed class LocalWebSecurityTests
{
    [Fact]
    public async Task UnauthenticatedClient_CannotReadStateOrCreateApproval()
    {
        await using var app = DashboardHost.Build([]);
        app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/state")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/approve-submit", new { approved = true })).StatusCode);
    }

    [Fact]
    public async Task ForeignOrigin_CannotUseBootstrapToken()
    {
        var options = new DashboardOptions();
        await using var app = DashboardHost.Build([], options);
        app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/session", new { token = options.BootstrapToken })).StatusCode);
    }

    [Fact]
    public async Task HostRebinding_IsRejected()
    {
        await using var app = DashboardHost.Build([]);
        app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
        client.DefaultRequestHeaders.Host = "attacker.invalid";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/health")).StatusCode);
    }
}
