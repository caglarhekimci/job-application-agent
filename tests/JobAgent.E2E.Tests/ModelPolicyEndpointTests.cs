using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JobAgent.Web;

namespace JobAgent.E2E.Tests;

public sealed class ModelPolicyEndpointTests
{
    [Fact]
    public async Task PolicyEndpointIsZeroPaidAndStrictAboutItsInputSchema()
    {
        var directory = Path.Combine(Path.GetTempPath(), "jobagent-model-policy-api-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var options = new DashboardOptions { DataDirectory = directory };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new(app.Urls.Single()) };
            client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
            var session = await client.PostAsJsonAsync("/api/session", new { token = options.BootstrapToken });
            var csrf = (await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("csrf").GetString();
            client.DefaultRequestHeaders.Add("X-JobAgent-Csrf", csrf);

            var initialResponse = await client.GetAsync("/api/workspace/model-policy");
            Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
            var initial = await initialResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("HostMediated", initial.GetProperty("mode").GetString());
            Assert.False(initial.GetProperty("paidApiEnabled").GetBoolean());
            Assert.Equal(4, initial.GetProperty("maxAnswerProposalOperationsPerApplication").GetInt32());
            Assert.DoesNotContain("token", initial.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("quota", initial.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cost", initial.GetRawText(), StringComparison.OrdinalIgnoreCase);

            var revision = initial.GetProperty("revision").GetInt64();
            var valid = await Post(client, $$"""
                {"expectedRevision":{{revision}},"mode":"Fixture","maxAnswerProposalOperationsPerApplication":2}
                """);
            Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

            var unknown = await Post(client, $$"""
                {"expectedRevision":{{revision + 1}},"mode":"Fixture","maxAnswerProposalOperationsPerApplication":2,"hostQuota":99}
                """);
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
            Assert.Contains("InvalidModelPolicyPayload", await unknown.Content.ReadAsStringAsync());

            var nullCap = await Post(client, $$"""
                {"expectedRevision":{{revision + 1}},"mode":"Fixture","maxAnswerProposalOperationsPerApplication":null}
                """);
            Assert.Equal(HttpStatusCode.BadRequest, nullCap.StatusCode);
            Assert.Contains("InvalidModelPolicyPayload", await nullCap.Content.ReadAsStringAsync());

            var api = await Post(client, $$"""
                {"expectedRevision":{{revision + 1}},"mode":"Api","maxAnswerProposalOperationsPerApplication":2}
                """);
            Assert.Equal(HttpStatusCode.Conflict, api.StatusCode);
            Assert.Contains("PaidApiDisabled", await api.Content.ReadAsStringAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string json) => client.PostAsync(
        "/api/workspace/model-policy", new StringContent(json, Encoding.UTF8, "application/json"));
}
