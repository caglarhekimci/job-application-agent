using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Bridge;
using JobAgent.Infrastructure.Workspace;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;

if (args.Length != 1) throw new ArgumentException("Expected an isolated synthetic runtime directory.");
var runtime = Path.GetFullPath(args[0]);
Directory.CreateDirectory(runtime);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
await using var trap = FakeCareerHost.Build("http://127.0.0.1:0");
await trap.StartAsync();
var options = new DashboardOptions { DataDirectory = runtime, EnableLocalCommands = true };
await using var dashboard = DashboardHost.Build([], options);
dashboard.Urls.Clear(); dashboard.Urls.Add("http://127.0.0.1:0"); await dashboard.StartAsync();
var origin = new Uri(dashboard.Urls.Single() + "/");
await HostBridgeRegistrationStore.WriteAsync(runtime, new(origin, options.BridgeToken, options.BridgeInstanceId, options.BridgeExpiresAt));
using var handler = new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false, UseProxy = false };
using var ui = new HttpClient(handler) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(30) };
ui.DefaultRequestHeaders.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
var workspace = dashboard.Services.GetRequiredService<LocalWorkspace>();
var initialVersion = 0;
Guid? initialJob = null;
try
{
    using var login = await ui.PostAsJsonAsync("api/session", new { token = options.BootstrapToken }, json);
    login.EnsureSuccessStatusCode();
    var session = await login.Content.ReadFromJsonAsync<JsonElement>(json);
    ui.DefaultRequestHeaders.Add("X-JobAgent-Csrf", session.GetProperty("csrf").GetString());
    Emit(new { ready = true, syntheticOnly = true, localMode = true, applicationApprovalGranted = false });
    while (await Console.In.ReadLineAsync() is { } line)
    {
        using var command = JsonDocument.Parse(line);
        var id = command.RootElement.GetProperty("id").GetInt32();
        var action = command.RootElement.GetProperty("action").GetString();
        try
        {
            if (action == "stop") { Emit(new { id, ok = true }); return; }
            if (action == "prepare")
            {
                if (initialVersion != 0) throw new InvalidOperationException("Fixture is already prepared.");
                using var content = new ByteArrayContent(Encoding.UTF8.GetBytes("Synthetic User\nProfessional C# developer 2020-2024"));
                using var upload = new HttpRequestMessage(HttpMethod.Post, "api/workspace/import") { Content = content };
                upload.Headers.Add("X-File-Name", "synthetic.txt");
                using var imported = await ui.SendAsync(upload);
                imported.EnsureSuccessStatusCode();
                var state = await workspace.GetAsync();
                using var profile = await ui.PostAsJsonAsync("api/workspace/profile", new ProfileReview
                {
                    ExpectedRevision = state.Revision, FullName = "Synthetic User", Email = "synthetic@example.invalid",
                    SalaryTarget = 100000, SalaryPrivateMinimum = 85000,
                    Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1),
                        Role = "Developer", Skills = ["C#"] }]
                }, json);
                profile.EnsureSuccessStatusCode();
                state = await workspace.GetAsync();
                using var job = await ui.PostAsJsonAsync("api/workspace/job", new JobReview
                {
                    ExpectedRevision = state.Revision, Employer = "Synthetic Employer", Title = "Developer",
                    Text = "Build local C# tools.", SourceUrl = trap.Urls.Single() + "/reviewed-job-never-fetch"
                }, json);
                job.EnsureSuccessStatusCode();
                initialVersion = (await workspace.GetAsync()).Profile.Version;
                initialJob = (await workspace.GetHostWorkspaceRefsAsync()).JobRef;
            }
            else if (action is not ("inspect" or "verify-final")) throw new InvalidOperationException("Unsupported fixture action.");

            var view = await workspace.GetAsync();
            var panel = await workspace.GetApplicationPanelAsync();
            var app = panel.Applications.SingleOrDefault();
            var hostState = app is null ? null : await workspace.GetApplicationStatusAsync(app.Draft.Id);
            var profileUnchanged = initialVersion > 0 && view.Profile.Version == initialVersion &&
                view.Profile.FullName == "Synthetic User" && view.Profile.Email == "synthetic@example.invalid" &&
                view.Profile.Salary.Target.Amount == 100000 && view.Profile.Salary.PrivateMinimum.Amount == 85000;
            var answerMemoryCount = view.Profile.Answers.Count;
            var receiptStore = trap.Services.GetRequiredService<ReceiptStore>();
            var profileProposalsPending = panel.ProfileProposals.All(p => p.Status == "Pending");
            var answerProposalsPending = app?.Proposals.All(p => p.ReviewStatus == JobAgent.Core.Answers.AnswerProposalReviewStatus.Proposed) ?? true;
            var resultVerified = false;
            if (action == "verify-final")
            {
                var expectedRef = command.RootElement.GetProperty("applicationRef").GetGuid();
                resultVerified = app?.Draft.Id == expectedRef && app.Draft.Status == ApplicationStatus.Cancelled &&
                    hostState is { SubmissionApproved: false, ReviewRequested: false, Evidence: null } &&
                    profileUnchanged && answerMemoryCount == 0 && panel.References.JobRef == initialJob &&
                    panel.ProfileProposals.Count == 1 && profileProposalsPending && panel.JobProposals.Count == 1 &&
                    app.Proposals.Count == 1 && answerProposalsPending && !app.Draft.Answers.ContainsKey("motivation") &&
                    receiptStore.Requests == 0 && receiptStore.SubmissionPosts == 0 && receiptStore.Receipts.Count == 0;
                if (!resultVerified) throw new InvalidOperationException("Final synthetic workspace invariants failed.");
            }
            Emit(new
            {
                id, ok = true, syntheticOnly = true, simulatedTrustedUiSetup = action == "prepare",
                applicationApprovalGranted = false, profileConfirmed = view.Profile.VerifiedAt is not null,
                profileUnchanged, answerMemoryCount, references = panel.References,
                applicationCount = panel.Applications.Count, applicationRef = app?.Draft.Id,
                state = app?.Draft.Status.ToString(), submissionApproved = hostState?.SubmissionApproved ?? false,
                profileProposalCount = panel.ProfileProposals.Count, profileProposalsPending,
                jobProposalCount = panel.JobProposals.Count, answerProposalCount = app?.Proposals.Count ?? 0,
                answerProposalsPending, jobUnchanged = panel.References.JobRef == initialJob,
                trapRequests = receiptStore.Requests, submissionPosts = receiptStore.SubmissionPosts,
                receiptCount = receiptStore.Receipts.Count, finalVerified = resultVerified,
                jobImportSourceUrl = trap.Urls.Single() + "/imported-job-never-fetch"
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        { Emit(new { id, ok = false, error = exception.GetType().Name }); }
    }
}
finally
{
    await HostBridgeRegistrationStore.RemoveIfOwnedAsync(runtime, options.BridgeInstanceId);
    await dashboard.StopAsync(); await trap.StopAsync();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}

void Emit(object value) => Console.WriteLine(JsonSerializer.Serialize(value, json));
