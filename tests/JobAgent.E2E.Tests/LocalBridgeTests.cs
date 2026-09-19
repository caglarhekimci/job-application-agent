using System.Net;
using System.Text;
using System.Text.Json;
using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Bridge;
using JobAgent.Infrastructure.Workspace;
using JobAgent.Mcp;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JobAgent.E2E.Tests;

public sealed class LocalBridgeTests
{
    [Fact]
    public async Task TwelveStdioToolsUseSameProtectedWorkspaceAndNeverSubmitOrApprove()
    {
        var root = TemporaryDirectory();
        try
        {
            await using var trap = FakeCareerHost.Build("http://127.0.0.1:0");
            await trap.StartAsync();
            var options = new DashboardOptions { DataDirectory = root, EnableLocalCommands = true };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            var workspace = app.Services.GetRequiredService<LocalWorkspace>();
            Assert.Same(workspace, app.Services.GetRequiredService<ILocalWorkspaceHost>());
            await Setup(workspace, trap.Urls.Single() + "/never-fetch");
            var before = await workspace.GetAsync();
            await HostBridgeRegistrationStore.WriteAsync(root, new(new Uri(app.Urls.Single()),
                options.BridgeToken, options.BridgeInstanceId, options.BridgeExpiresAt));
            await using var client = await StartAsync(root);
            Assert.Equal(12, (await client.ListToolsAsync()).Count);
            var cap = Structured(await client.CallToolAsync("runtime_get_capabilities"));
            Assert.Equal("HostMediated", cap.GetProperty("mode").GetString());
            Assert.False(cap.GetProperty("canMintApproval").GetBoolean());
            var refs = cap.GetProperty("workspace");
            var profileRef = refs.GetProperty("profileRef").GetString()!;
            var jobRef = refs.GetProperty("jobRef").GetString()!;
            var resumeRef = refs.GetProperty("resumeRef").GetString()!;
            var summary = Structured(await Call(client, "profile_get_summary", ("profileRef", profileRef)));
            Assert.Contains("C#", summary.GetRawText());
            foreach (var privateValue in new[] { "85000", "100000", "Synthetic User", "synthetic@example.invalid", "2020-2024" })
                Assert.DoesNotContain(privateValue, summary.GetRawText());
            var evidence = summary.GetProperty("evidence")[0].GetProperty("evidenceRef").GetString()!;
            var patch = Structured(await Call(client, "profile_propose_patch", ("profileRef", profileRef),
                ("baseVersion", before.Profile.Version), ("changes", new[] { new { field = "FullName", value = "Pending name", evidenceRefs = Array.Empty<string>() } })));
            Assert.Equal("Pending", patch.GetProperty("status").GetString());
            Assert.Equal(before.Profile.FullName, (await workspace.GetAsync()).Profile.FullName);
            Assert.Equal(before.Profile.Version, (await workspace.GetAsync()).Profile.Version);
            var imported = Structured(await Call(client, "job_import_text", ("text", "Required: 2 years of professional C# experience."),
                ("sourceUrl", trap.Urls.Single() + "/never-fetch")));
            Assert.True(imported.GetProperty("requiresUserReview").GetBoolean());
            Assert.Equal(before.Job!.Id, (await workspace.GetAsync()).Job!.Id);
            var evaluation = Structured(await Call(client, "job_evaluate", ("jobRef", imported.GetProperty("jobRef").GetString()), ("profileRef", profileRef)));
            Assert.Equal("ReviewNeeded", evaluation.GetProperty("evaluation").GetProperty("status").GetString());
            var created = Structured(await Call(client, "application_create_draft", ("jobRef", jobRef), ("profileRef", profileRef), ("resumeRef", resumeRef)));
            var applicationRef = created.GetProperty("applicationRef").GetString()!;
            Assert.Equal("NeedsInput", created.GetProperty("status").GetString());
            var questions = Structured(await Call(client, "application_get_questions", ("applicationRef", applicationRef)));
            Assert.Contains("motivation", questions.GetRawText());
            Assert.DoesNotContain("85000", questions.GetRawText());
            var revision = questions.GetProperty("revision").GetInt64();
            var answerArguments = new (string, object?)[]
            {
                ("applicationRef", applicationRef), ("baseRevision", revision), ("evidenceRefs", new[] { evidence }),
                ("answers", new[] { new { schemaVersion = 1, semanticKey = "motivation", proposedValue = "I want to build C# tools.",
                    language = "tr", evidenceIds = new[] { evidence }, rationale = "Requires user review." } })
            };
            var proposals = Structured(await Call(client, "application_propose_answers", answerArguments));
            Assert.Equal("RequiresReview", proposals.GetProperty("results")[0].GetProperty("disposition").GetString());
            Assert.Empty((await workspace.GetAsync()).Profile.Answers);
            var conflict = await Assert.ThrowsAsync<McpProtocolException>(() => Call(client, "application_propose_answers", answerArguments));
            Assert.Contains("ApplicationRevisionConflict", conflict.Message);
            var review = Structured(await Call(client, "application_prepare_review", ("applicationRef", applicationRef)));
            Assert.True(review.GetProperty("reviewRequested").GetBoolean());
            Assert.False(review.GetProperty("submissionApproved").GetBoolean());
            var denied = await Assert.ThrowsAsync<McpProtocolException>(() => Call(client, "application_execute_approved", ("applicationRef", applicationRef)));
            Assert.Contains("BlockedPermission", denied.Message);
            var cancelled = Structured(await Call(client, "application_cancel", ("applicationRef", applicationRef)));
            Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
            var status = Structured(await Call(client, "application_get_status", ("applicationRef", applicationRef)));
            Assert.Equal("Cancelled", status.GetProperty("status").GetString());
            Assert.Contains(applicationRef, await workspace.ExportAsync());
            Assert.Equal(0, trap.Services.GetRequiredService<ReceiptStore>().Requests);
            foreach (var output in new[] { summary, created, questions, proposals, review, cancelled, status })
            {
                Assert.DoesNotContain(options.BridgeToken, output.GetRawText());
                Assert.DoesNotContain("85000", output.GetRawText());
                Assert.DoesNotContain("synthetic@example.invalid", output.GetRawText());
            }
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public async Task OnlyAllowlistedJsonRoutesAcceptBoundedStrictBodiesAndNeverUiCredentials()
    {
        var root = TemporaryDirectory();
        try
        {
            var options = new DashboardOptions { DataDirectory = root, EnableLocalCommands = true };
            await using var app = DashboardHost.Build([], options);
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            const string route = "/internal/mcp/local/v1/jobs/import";
            using (var denied = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonBody("{\"text\":\"Job\"}") })
            {
                denied.Headers.Add("X-JobAgent-Bridge", options.BootstrapToken);
                Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(denied)).StatusCode);
            }
            http.DefaultRequestHeaders.Add("X-JobAgent-Bridge", options.BridgeToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("/internal/mcp/applications", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.PostAsync("/api/approve-submit", null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync("/internal/mcp/local/v1/refs", JsonBody("{}"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsync(route + "?approved=true", JsonBody("{\"text\":\"Job\"}"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync("/internal/mcp/local/v1/approval", null)).StatusCode);
            foreach (var json in new[]
            {
                "{\"text\":\"Job\",\"approved\":true}", "{\"text\":\"First\",\"text\":\"Second\"}",
                "{\"Text\":\"Job\"}", "{\"text\":null}", "{\"text\":\"Job\",\"sourceUrl\":{\"url\":\"x\"}}"
            })
            {
                var response = await http.PostAsync(route, JsonBody(json));
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Contains("InvalidArguments", await response.Content.ReadAsStringAsync());
            }
            var reference = Guid.NewGuid().ToString("D");
            var nested = "{\"profileRef\":\"" + reference + "\",\"baseVersion\":1,\"changes\":[{\"field\":\"FullName\",\"value\":\"name\",\"evidenceRefs\":[],\"verified\":true}]}";
            Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsync("/internal/mcp/local/v1/profiles/" + reference + "/patches", JsonBody(nested))).StatusCode);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await http.PostAsync(route, JsonBody(new string('x', LocalMcpEndpoints.MaximumBytes + 1)))).StatusCode);
            using var origin = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonBody("{\"text\":\"Job\"}") };
            origin.Headers.Add("Origin", app.Urls.Single());
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(origin)).StatusCode);
            Assert.Equal(0, (await app.Services.GetRequiredService<LocalWorkspace>().GetAsync()).Revision);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void LocalAndSyntheticModesCannotBeCombined()
    {
        Assert.Throws<ArgumentException>(() => new RuntimeStore(Path.GetTempPath(), true, true));
        Assert.Throws<ArgumentException>(() => DashboardHost.Build([], new() { EnableLocalCommands = true, EnableSyntheticCommands = true }));
    }

    [Theory]
    [InlineData("wrong-reference")]
    [InlineData("private-extra")]
    [InlineData("duplicate")]
    [InlineData("oversized")]
    [InlineData("null-reason")]
    [InlineData("wrong-review-reference")]
    public async Task BridgeRejectsMalformedResponsesWithoutReturningUntrustedPayload(string mutation)
    {
        var root = TemporaryDirectory();
        try
        {
            var reference = Guid.NewGuid();
            var profileRef = Guid.NewGuid();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var app = builder.Build();
            var valid = JsonSerializer.Serialize(new { profileRef = reference, version = 1, verifiedSkills = new[] { "C#" }, evidence = Array.Empty<object>() });
            var response = mutation switch
            {
                "wrong-reference" => valid.Replace(reference.ToString("D"), Guid.NewGuid().ToString("D"), StringComparison.Ordinal),
                "private-extra" => valid[..^1] + ",\"salaryPrivateMinimum\":85000}",
                "duplicate" => valid[..^1] + ",\"profileRef\":\"" + reference + "\"}",
                "oversized" => new string('x', LocalMcpEndpoints.MaximumBytes + 1),
                "null-reason" => JsonSerializer.Serialize(new
                {
                    jobRef = reference,
                    profileRef,
                    profileVersion = 1,
                    revision = 1,
                    evaluation = new
                    {
                        status = "ReviewNeeded",
                        reason = "review",
                        requirements = new[] { new {
                        requirementText = "C#", requirementType = "Skill", candidateEvidenceIds = Array.Empty<string>(), assessment = "Unknown", reason = (string?)null } }
                    }
                }),
                "wrong-review-reference" => JsonSerializer.Serialize(new
                {
                    applicationRef = reference,
                    revision = 1,
                    status = "NeedsInput",
                    reviewRequested = true,
                    submissionApproved = false,
                    reviewRef = "local-application:" + Guid.NewGuid(),
                    unresolvedQuestions = 1,
                    evidence = (object?)null
                }),
                _ => throw new InvalidOperationException()
            };
            app.MapPost("/{**path}", () => Results.Text(response, "application/json"));
            await app.StartAsync();
            var token = new string('A', 64);
            await HostBridgeRegistrationStore.WriteAsync(root, new(new Uri(app.Urls.Single()), token, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(5)));
            var client = new WorkspaceHostBridgeClient(new RuntimeStore(root, enableLocalCommands: true));
            var error = await Assert.ThrowsAsync<McpProtocolException>(async () =>
            {
                if (mutation == "null-reason") await client.EvaluateJobAsync(reference, profileRef);
                else if (mutation == "wrong-review-reference") await client.PrepareApplicationReviewAsync(reference);
                else await client.GetHostProfileSummaryAsync(reference);
            });
            Assert.Equal("InvalidBridgeResponse", error.Message);
            Assert.DoesNotContain("85000", error.Message);
        }
        finally { Cleanup(root); }
    }

    private static async Task Setup(LocalWorkspace workspace, string sourceUrl)
    {
        var imported = await workspace.ImportAsync(new MemoryStream("Synthetic User\nProfessional C# developer 2020-2024"u8.ToArray()), "synthetic.txt");
        var reviewed = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = imported.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid",
            SalaryTarget = 100000,
            SalaryPrivateMinimum = 85000,
            Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1), Role = "Developer", Skills = ["C#"] }]
        });
        await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = reviewed.Revision,
            Employer = "Synthetic Employer",
            Title = "Developer",
            Text = "Build local C# tools.",
            SourceUrl = sourceUrl
        });
    }
    private static async Task<McpClient> StartAsync(string root)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")!;
        var env = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
        env["DOTNET_ROOT"] = dotnetRoot; env["JOBAGENT_RUNTIME_DIR"] = root;
        env["JOBAGENT_ENABLE_LOCAL_COMMANDS"] = "1"; env["JOBAGENT_ENABLE_SYNTHETIC_COMMANDS"] = "0";
        return await McpClient.CreateAsync(new StdioClientTransport(new()
        {
            Name = "local-workspace-stdio",
            Command = Path.Combine(dotnetRoot, "dotnet.exe"),
            Arguments = [typeof(ReadOnlyTools).Assembly.Location],
            InheritEnvironmentVariables = false,
            EnvironmentVariables = env,
            ShutdownTimeout = TimeSpan.FromSeconds(2)
        }));
    }
    private static Task<CallToolResult> Call(McpClient client, string name, params (string Key, object? Value)[] args) =>
        client.CallToolAsync(name, args.ToDictionary(a => a.Key, a => a.Value)).AsTask();
    private static JsonElement Structured(CallToolResult result)
    { Assert.NotEqual(true, result.IsError); return result.StructuredContent!.Value; }
    private static StringContent JsonBody(string text) => new(text, Encoding.UTF8, "application/json");
    private static string TemporaryDirectory() => Path.Combine(Path.GetTempPath(), "jobagent-local-bridge-" + Guid.NewGuid());
    private static void Cleanup(string root)
    { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
