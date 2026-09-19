using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace JobAgent.E2E.Tests;

public sealed class AdversarialFormTests
{
    [Fact]
    public async Task ManualChallenge_PersistsPausedStateAcrossRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-manual-takeover-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0",
                new(ManualChallenge: ManualChallengeKind.Captcha, ManualChallengeAfterResumeMilliseconds: 750));
            await site.StartAsync();
            await using (var workflow = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo()))
            {
                await workflow.LoadFixtureAsync(); await workflow.ConfirmProfileFromUiAsync();
                await workflow.CreateDraftAsync(); await workflow.ShareAndFillFromUiAsync("synthetic-session");
                await Task.Delay(1000);
                await workflow.SubmitFromUiAsync("synthetic-session");
                var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
                var paused = Assert.Single(await journal.ListAsync());
                Assert.Equal(ApplicationStatus.NeedsInput, paused.Draft.Status);
                Assert.Equal("ManualTakeoverRequired", paused.Error);
                Assert.NotNull(paused.Sharing?.UsedAt);
                Assert.Null(paused.Submission);
                await Assert.ThrowsAsync<PolicyException>(() => workflow.SubmitFromUiAsync("synthetic-session"));
            }
            await using (var restarted = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo()))
            {
                await restarted.GetStateAsync();
                var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
                var paused = Assert.Single(await journal.ListAsync());
                Assert.Equal(ApplicationStatus.NeedsInput, paused.Draft.Status);
                Assert.Equal("ManualTakeoverRequired", paused.Error);
            }
            Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task WebSocketEgress_IsBlockedBeforeHandshake()
    {
        await using var trap = FakeCareerHost.Build("http://127.0.0.1:0");
        await trap.StartAsync();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0",
            new(WebSocketTarget: trap.Urls.Single().Replace("http:", "ws:") + "/socket"));
        await site.StartAsync();
        var draft = BrowserPolicyTests.Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        var denied = await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));
        Assert.Equal("RecipientChanged", denied.Code);
        Assert.Equal(0, trap.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    [Fact]
    public async Task TamperedOutgoingAnswers_NeverReachServer()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(TamperSalaryOnSubmit: true));
        await site.StartAsync();
        var draft = BrowserPolicyTests.Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await browser.PrepareAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume);
        SubmissionEvidence? evidence = null;
        try { evidence = await browser.SubmitAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.Submit)); }
        catch (PolicyException) { }
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        Assert.Null(evidence);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":42}")]
    public async Task MalformedReceipt_RemainsUncertain_AndCannotBeCancelled(string response)
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-malformed-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(MalformedReceipt: response));
            await site.StartAsync();
            await using var workflow = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo());
            await workflow.LoadFixtureAsync(); await workflow.ConfirmProfileFromUiAsync();
            await workflow.CreateDraftAsync(); await workflow.ShareAndFillFromUiAsync("synthetic-session");
            await workflow.SubmitFromUiAsync("synthetic-session");
            await Assert.ThrowsAsync<PolicyException>(() => workflow.CancelAsync());
            var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
            var run = Assert.Single(await journal.ListAsync());
            Assert.Equal(ApplicationStatus.SubmittedUnverified, run.Draft.Status);
            Assert.NotNull(run.Submission!.UsedAt);
            Assert.Null(run.Evidence);
            Assert.Equal(1, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SharingApproval_IsConsumed_AndSessionNeverAppearsInUiState()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-sharing-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
            await site.StartAsync();
            await using var workflow = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo());
            await workflow.LoadFixtureAsync(); await workflow.ConfirmProfileFromUiAsync();
            await workflow.CreateDraftAsync(); await workflow.ShareAndFillFromUiAsync("secret-synthetic-session");
            var state = System.Text.Json.JsonSerializer.Serialize(await workflow.GetStateAsync());
            Assert.DoesNotContain("secret-synthetic-session", state);
            Assert.DoesNotContain("UserSessionId", state);
            var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
            var run = Assert.Single(await journal.ListAsync());
            Assert.NotNull(run.Sharing!.UsedAt);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task FormScript_CannotSubmitDuringDataSharing()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(AutoSubmitOnInput: true));
        await site.StartAsync();
        var draft = BrowserPolicyTests.Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        try { await browser.PrepareAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume); }
        catch (PolicyException) { }
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Fact]
    public async Task RedirectToNewRecipient_StopsBeforeNewOriginRequest()
    {
        await using var trap = FakeCareerHost.Build("http://127.0.0.1:0");
        await trap.StartAsync();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(RedirectTarget: trap.Urls.Single() + "/jobs/synthetic-dotnet"));
        await site.StartAsync();
        var draft = BrowserPolicyTests.Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));
        Assert.Equal(0, trap.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    [Fact]
    public async Task NewRequiredQuestion_InvalidatesPreparedPackage()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(AddRequiredQuestion: true));
        await site.StartAsync();
        var draft = BrowserPolicyTests.Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Fact]
    public async Task PostSubmitNetworkDrop_IsUnknown_AndNeverRetriesEvenAfterRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "jobagent-drop-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(DropSubmissionResponse: true));
            await site.StartAsync();
            await using (var workflow = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo()))
            {
                await workflow.LoadFixtureAsync(); await workflow.ConfirmProfileFromUiAsync();
                await workflow.CreateDraftAsync(); await workflow.ShareAndFillFromUiAsync("synthetic-session");
                await workflow.SubmitFromUiAsync("synthetic-session");
                var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
                var run = Assert.Single(await journal.ListAsync());
                Assert.Equal(ApplicationStatus.SubmittedUnverified, run.Draft.Status);
                Assert.NotNull(run.Submission!.UsedAt);
                Assert.Null(run.Evidence);
                await Assert.ThrowsAsync<PolicyException>(() => workflow.SubmitFromUiAsync("synthetic-session"));
            }
            await using (var restarted = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo()))
            {
                await restarted.GetStateAsync();
                await Assert.ThrowsAsync<PolicyException>(() => restarted.SubmitFromUiAsync("synthetic-session"));
            }
            var store = site.Services.GetRequiredService<ReceiptStore>();
            Assert.Single(store.Receipts);
            Assert.Equal(1, store.SubmissionPosts);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }
}
