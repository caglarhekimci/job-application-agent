using System.Net;
using JobAgent.Web;

namespace JobAgent.E2E.Tests;

public sealed class HostBridgeBoundaryTests
{
    [Fact]
    public void EqualUiAndBridgeCredentialsAreRejectedAtStartup()
    {
        var token = new string('A', 64);
        Assert.Throws<ArgumentException>(() => DashboardHost.Build([], new DashboardOptions
        { EnableSyntheticCommands = true, BridgeToken = token, BootstrapToken = token }));
    }

    [Fact]
    public async Task BridgeCredentialCannotApproveAndUiCredentialCannotCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-bridge-web-" + Guid.NewGuid());
        var options = new DashboardOptions { DataDirectory = root, EnableSyntheticCommands = true };
        try
        {
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var token in new[] { "", "wrong", options.BootstrapToken })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/mcp/applications");
                request.Headers.Add("X-JobAgent-Bridge", token);
                Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(request)).StatusCode);
            }
            http.DefaultRequestHeaders.Add("X-JobAgent-Bridge", options.BridgeToken);
            var allowed = await http.PostAsync("/internal/mcp/applications", null);
            Assert.Equal(HttpStatusCode.Conflict, allowed.StatusCode);
            Assert.Contains("ProfileReviewRequired", await allowed.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/approve-submit", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("/internal/mcp/applications",
                new StringContent("{\"approval\":true}"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("/internal/mcp/applications?approval=true", null)).StatusCode);
            using var foreign = new HttpRequestMessage(HttpMethod.Post, "/internal/mcp/applications");
            foreign.Headers.Host = "evil.invalid";
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(foreign)).StatusCode);
            using var origin = new HttpRequestMessage(HttpMethod.Post, "/internal/mcp/applications");
            origin.Headers.Add("Origin", app.Urls.Single());
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(origin)).StatusCode);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DisabledOrExpiredBridgeCannotCommand()
    {
        foreach (var enabled in new[] { false, true })
        {
            var root = Path.Combine(Path.GetTempPath(), "jobagent-bridge-off-" + Guid.NewGuid());
            var options = new DashboardOptions
            {
                DataDirectory = root,
                EnableSyntheticCommands = enabled,
                BridgeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            };
            try
            {
                await using var app = DashboardHost.Build([], options);
                app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
                using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
                http.DefaultRequestHeaders.Add("X-JobAgent-Bridge", options.BridgeToken);
                Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("/internal/mcp/applications", null)).StatusCode);
            }
            finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
