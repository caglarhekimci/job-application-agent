using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace JobAgent.E2E.Tests;

public sealed class HostCoordinatorTests : IDisposable
{
    private readonly List<string> roots = [];
    [Fact]
    public async Task HostCreate_RequiresUiConfirmedProfile_AndBindsCurrentGuid()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        await using var workflow = NewWorkflow(TemporaryDirectory(), site.Urls.Single());

        var missing = await Assert.ThrowsAsync<PolicyException>(() => workflow.CreateSyntheticDraftForHostAsync());
        Assert.Equal("ProfileReviewRequired", missing.Code);

        await workflow.LoadFixtureAsync();
        await workflow.ConfirmProfileFromUiAsync();
        var created = await workflow.CreateSyntheticDraftForHostAsync();
        Assert.Equal(ApplicationStatus.ReadyForDataSharing, created.Status);
        Assert.False(created.ReviewRequested);
        Assert.False(created.SubmissionApproved);
        Assert.Null(created.ReceiptId);
        var wrong = await Assert.ThrowsAsync<PolicyException>(() => workflow.GetHostStatusAsync(Guid.NewGuid()));
        Assert.Equal("ApplicationNotFound", wrong.Code);
    }

    [Fact]
    public async Task ExecuteWithoutStoredUiApproval_IsDeniedWithoutMutation()
    {
        var root = TemporaryDirectory();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        await using var workflow = NewWorkflow(root, site.Urls.Single());
        var created = await CreateReviewedAsync(workflow);
        await workflow.ShareAndFillFromUiAsync("trusted-ui-session");

        var denied = await Assert.ThrowsAsync<PolicyException>(() =>
            workflow.ExecuteAlreadyApprovedAsync(created.ApplicationRef));

        Assert.Equal("ConsentRequired", denied.Code);
        var state = await workflow.GetHostStatusAsync(created.ApplicationRef);
        Assert.Equal(ApplicationStatus.AwaitingSubmissionApproval, state.Status);
        Assert.False(state.SubmissionApproved);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        var record = await new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"))
            .GetAsync(created.ApplicationRef);
        Assert.Null(record!.Submission);
    }

    [Fact]
    public async Task UiApprovalThenConcurrentHostExecute_ProducesOnePostAndOneReceipt()
    {
        var root = TemporaryDirectory();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        await using var workflow = NewWorkflow(root, site.Urls.Single());
        var created = await CreateReviewedAsync(workflow);
        await workflow.ShareAndFillFromUiAsync("trusted-ui-session");
        var approved = await workflow.ApproveForHostFromUiAsync(created.ApplicationRef, "trusted-ui-session");
        Assert.True(approved.SubmissionApproved);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);

        var results = await Task.WhenAll(
            workflow.ExecuteAlreadyApprovedAsync(created.ApplicationRef),
            workflow.ExecuteAlreadyApprovedAsync(created.ApplicationRef));

        Assert.All(results, result => Assert.Equal(ApplicationStatus.SubmittedVerified, result.Status));
        Assert.All(results, result => Assert.False(result.SubmissionApproved));
        Assert.All(results, result => Assert.StartsWith("SYN-", result.ReceiptId));
        Assert.Equal(1, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        Assert.Single(site.Services.GetRequiredService<ReceiptStore>().Receipts);
    }

    [Fact]
    public async Task LateChallengeBeforeClaim_PausesAndClearsStoredSubmissionApproval()
    {
        var root = TemporaryDirectory();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        var browser = new LateChallengeBrowserSession(new(site.Urls.Single()));
        await using var workflow = new DemoWorkflow(root, site.Urls.Single(), HappyPathTests.FindRepo(),
            browserFactory: _ => browser);
        var created = await CreateReviewedAsync(workflow);
        await workflow.ShareAndFillFromUiAsync("trusted-ui-session");
        var approved = await workflow.ApproveForHostFromUiAsync(created.ApplicationRef, "trusted-ui-session");
        Assert.Equal(ApplicationStatus.AwaitingSubmissionApproval, approved.Status);
        Assert.True(approved.SubmissionApproved);
        Assert.Equal(1, browser.ReadinessChecks);
        var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
        var beforeChallenge = await journal.GetAsync(created.ApplicationRef);
        Assert.NotNull(beforeChallenge!.Submission);
        Assert.Null(beforeChallenge.Submission.UsedAt);
        browser.ChallengeRequired = true;

        var paused = await workflow.ExecuteAlreadyApprovedAsync(created.ApplicationRef);

        Assert.Equal(ApplicationStatus.NeedsInput, paused.Status);
        Assert.False(paused.SubmissionApproved);
        Assert.Equal(2, browser.ReadinessChecks);
        Assert.Equal(0, browser.SubmitCalls);
        Assert.True(browser.Disposed);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
        var record = await journal.GetAsync(created.ApplicationRef);
        Assert.Null(record!.Submission);
        Assert.Null(record.Evidence);
        Assert.Equal("ManualTakeoverRequired", record.Error);
    }

    [Fact]
    public async Task RestartClearsStoredSubmissionApprovalBeforeNewSharing()
    {
        var root = TemporaryDirectory();
        Guid applicationRef;
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        await using (var first = NewWorkflow(root, site.Urls.Single()))
        {
            var created = await CreateReviewedAsync(first);
            applicationRef = created.ApplicationRef;
            await first.ShareAndFillFromUiAsync("first-ui-session");
            var approved = await first.ApproveForHostFromUiAsync(applicationRef, "first-ui-session");
            Assert.True(approved.SubmissionApproved);
        }

        await using var restarted = NewWorkflow(root, site.Urls.Single());
        var recovered = await restarted.GetHostStatusAsync(applicationRef);

        Assert.Equal(ApplicationStatus.ReadyForDataSharing, recovered.Status);
        Assert.False(recovered.SubmissionApproved);
        var record = await new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"))
            .GetAsync(applicationRef);
        Assert.Null(record!.Submission);
        Assert.NotNull(record.Sharing?.UsedAt);
        await restarted.ShareAndFillFromUiAsync("second-ui-session");
        var refilled = await restarted.GetHostStatusAsync(applicationRef);
        Assert.Equal(ApplicationStatus.AwaitingSubmissionApproval, refilled.Status);
        Assert.False(refilled.SubmissionApproved);
    }

    [Fact]
    public async Task ExistingDirectUiSubmit_UsesSameSingleClaimExecutionPath()
    {
        var root = TemporaryDirectory();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        await using var workflow = NewWorkflow(root, site.Urls.Single());
        var created = await CreateReviewedAsync(workflow);
        await workflow.ShareAndFillFromUiAsync("trusted-ui-session");

        await workflow.SubmitFromUiAsync("trusted-ui-session");

        var result = await workflow.GetHostStatusAsync(created.ApplicationRef);
        Assert.Equal(ApplicationStatus.SubmittedVerified, result.Status);
        Assert.StartsWith("SYN-", result.ReceiptId);
        Assert.Equal(1, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    private static DemoWorkflow NewWorkflow(string root, string origin) =>
        new(root, origin, HappyPathTests.FindRepo());

    // The real browser still prepares and validates the form. Only the challenge event's
    // position is controlled here; BrowserPolicyTests separately exercise real MFA/CAPTCHA DOM.
    private sealed class LateChallengeBrowserSession(Uri origin) : IBrowserSession
    {
        private readonly ManagedBrowserSession inner = new(origin);
        public bool ChallengeRequired { get; set; }
        public int ReadinessChecks { get; private set; }
        public int SubmitCalls { get; private set; }
        public bool Disposed { get; private set; }

        public Task PrepareAsync(ApplicationDraft draft, ApprovalReceipt? sharing, ResumeDocument resume,
            CancellationToken ct = default) => inner.PrepareAsync(draft, sharing, resume, ct);

        public async Task EnsureReadyForSubmissionAsync(ApplicationDraft draft, CancellationToken ct = default)
        {
            ReadinessChecks++;
            await inner.EnsureReadyForSubmissionAsync(draft, ct);
            if (ChallengeRequired) throw new PolicyException("ManualTakeoverRequired");
        }

        public Task<SubmissionEvidence?> SubmitAsync(ApplicationDraft draft, ApprovalReceipt? approval,
            CancellationToken ct = default)
        {
            SubmitCalls++;
            return inner.SubmitAsync(draft, approval, ct);
        }

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            Disposed = true;
        }
    }

    private static async Task<HostApplicationSummary> CreateReviewedAsync(DemoWorkflow workflow)
    {
        await workflow.LoadFixtureAsync();
        await workflow.ConfirmProfileFromUiAsync();
        var created = await workflow.CreateSyntheticDraftForHostAsync();
        var requested = await workflow.RequestHostReviewAsync(created.ApplicationRef);
        Assert.True(requested.ReviewRequested);
        return requested;
    }

    private string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "jobagent-host-coordinator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        roots.Add(path);
        return path;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var root in roots)
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
