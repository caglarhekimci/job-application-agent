using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Bridge;
using JobAgent.Web;
using Microsoft.Extensions.DependencyInjection;

if (args.Length != 1) throw new ArgumentException("Expected an isolated synthetic runtime directory.");
var runtimeDirectory = Path.GetFullPath(args[0]);
Directory.CreateDirectory(runtimeDirectory);
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
await site.StartAsync();
var options = new DashboardOptions
{
    DataDirectory = runtimeDirectory,
    CareerOrigin = site.Urls.Single(),
    EnableSyntheticCommands = true
};
await using var dashboard = DashboardHost.Build([], options);
dashboard.Urls.Clear();
dashboard.Urls.Add("http://127.0.0.1:0");
await dashboard.StartAsync();
var dashboardOrigin = new Uri(dashboard.Urls.Single() + "/");
await HostBridgeRegistrationStore.WriteAsync(runtimeDirectory,
    new(dashboardOrigin, options.BridgeToken, options.BridgeInstanceId, options.BridgeExpiresAt));
using var handler = new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false };
using var ui = new HttpClient(handler) { BaseAddress = dashboardOrigin, Timeout = TimeSpan.FromSeconds(45) };
ui.DefaultRequestHeaders.Add("Origin", dashboardOrigin.GetLeftPart(UriPartial.Authority));
try
{
    using var login = await ui.PostAsJsonAsync("api/session", new { token = options.BootstrapToken });
    login.EnsureSuccessStatusCode();
    var session = await login.Content.ReadFromJsonAsync<JsonElement>();
    ui.DefaultRequestHeaders.Add("X-JobAgent-Csrf", session.GetProperty("csrf").GetString());
    // Credentials stay in this trusted synthetic fixture process, never in the model or logs.
    Console.WriteLine(JsonSerializer.Serialize(new { ready = true, synthetic = true }, jsonOptions));
    while (await Console.In.ReadLineAsync() is { } line)
    {
        var command = JsonSerializer.Deserialize<JsonElement>(line);
        var id = command.GetProperty("id").GetInt32();
        var action = command.GetProperty("action").GetString();
        try
        {
            switch (action)
            {
                case "prepare-profile":
                    await Post("api/demo/load");
                    await Post("api/profile/confirm");
                    break;
                case "share-fill":
                    await Post("api/approve-share");
                    break;
                case "approve-host":
                    var reference = Guid.Parse(command.GetProperty("applicationRef").GetString()!);
                    await Post("api/approve-host-submit/" + reference.ToString("D"));
                    break;
                case "inspect":
                case "verify-final":
                    break;
                case "stop":
                    Console.WriteLine(JsonSerializer.Serialize(new { id, ok = true }, jsonOptions));
                    return;
                default:
                    throw new InvalidOperationException("Unsupported fixture command.");
            }
            var state = await ui.GetFromJsonAsync<JsonElement>("api/state");
            var application = state.GetProperty("application");
            var hasApplication = application.ValueKind == JsonValueKind.Object;
            var draft = hasApplication ? application.GetProperty("draft") : default;
            var evidence = hasApplication ? application.GetProperty("evidence") : default;
            var receipts = site.Services.GetRequiredService<ReceiptStore>();
            var receipt = receipts.Receipts.SingleOrDefault();
            if (action == "verify-final")
            {
                if (!hasApplication || draft.GetProperty("status").GetString() != "SubmittedVerified"
                    || receipts.SubmissionPosts != 1 || receipts.Receipts.Count != 1 || receipt is null
                    || receipt.Salary != "100000" || receipt.ProfessionalYears != "3"
                    || receipt.FileName != "synthetic-resume.txt"
                    || receipt.ResumeHash != state.GetProperty("resumeHash").GetString()
                    || receipt.ResumeHash != draft.GetProperty("resumeHash").GetString()
                    || receipt.ApplicationKey != draft.GetProperty("id").GetString()
                    || evidence.GetProperty("receiptId").GetString() != receipt.Id)
                    throw new InvalidOperationException("Exactly one verified synthetic receipt with approved answers and resume hash was required.");
            }
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                id,
                ok = true,
                synthetic = true,
                simulatedTrustedUi = action is "prepare-profile" or "share-fill" or "approve-host",
                profileConfirmed = state.GetProperty("profileConfirmed").GetBoolean(),
                applicationRef = hasApplication ? draft.GetProperty("id").GetString() : null,
                state = hasApplication ? draft.GetProperty("status").GetString() : null,
                reviewRequested = hasApplication && application.GetProperty("hostReviewRequested").GetBoolean(),
                submissionApproved = hasApplication && application.GetProperty("submissionApproved").GetBoolean(),
                submissionPosts = receipts.SubmissionPosts,
                receiptCount = receipts.Receipts.Count,
                receiptId = receipt?.Id,
                resumeHash = receipt?.ResumeHash,
                finalVerified = action == "verify-final"
            }, jsonOptions));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            // Avoid serializing HTTP headers, bootstrap tokens, CSRF values or arbitrary input.
            Console.WriteLine(JsonSerializer.Serialize(new { id, ok = false, error = exception.GetType().Name }, jsonOptions));
        }
    }
}
finally
{
    await HostBridgeRegistrationStore.RemoveIfOwnedAsync(runtimeDirectory, options.BridgeInstanceId);
    await dashboard.StopAsync();
    await site.StopAsync();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}

async Task Post(string path)
{
    using var response = await ui.PostAsync(path, null);
    response.EnsureSuccessStatusCode();
}
