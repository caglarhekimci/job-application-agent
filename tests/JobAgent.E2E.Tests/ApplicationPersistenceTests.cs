using JobAgent.Core.Applications;
using JobAgent.Infrastructure.Applications;

namespace JobAgent.E2E.Tests;

public sealed class ApplicationPersistenceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-tests-" + Guid.NewGuid());
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private async Task<ApplicationJournal> Store()
    {
        Directory.CreateDirectory(root);
        var store = new ApplicationJournal(Path.Combine(root, "applications.db"));
        await store.InitializeAsync();
        return store;
    }
    private static WorkflowRecord Ready()
    {
        var draft = new ApplicationDraft
        {
            JobKey = "synthetic:one",
            Synthetic = true,
            ResumeHash = "abc",
            RecipientOrigin = "http://127.0.0.1:5179",
            ProfileVersion = 1,
            Status = ApplicationStatus.AwaitingSubmissionApproval
        };
        return new(draft, Submission: ApprovalPolicy.GrantFromUserInterface(draft,
            ApprovalPurpose.Submit, "session", Now));
    }

    [Fact]
    public async Task Application_SurvivesRestart()
    {
        var record = Ready();
        await (await Store()).CreateAsync(record);
        var restored = await (await Store()).GetAsync(record.Draft.Id);
        Assert.NotNull(restored);
        Assert.Equal(record.Draft.PayloadHash(), restored.Draft.PayloadHash());
        Assert.Equal(record.Submission, restored.Submission);
    }

    [Fact]
    public async Task DuplicateJob_IsRejected()
    {
        var store = await Store();
        await store.CreateAsync(Ready());
        await Assert.ThrowsAsync<PolicyException>(() => store.CreateAsync(Ready()));
    }

    [Fact]
    public async Task ConcurrentClaims_OnlyOneSucceeds()
    {
        var record = Ready();
        var store = await Store();
        await store.CreateAsync(record);
        var claims = await Task.WhenAll(store.ClaimSubmissionAsync(record.Draft.Id, Now),
            (await Store()).ClaimSubmissionAsync(record.Draft.Id, Now));
        Assert.Single(claims, x => x);
        var saved = await store.GetAsync(record.Draft.Id);
        Assert.NotNull(saved);
        Assert.Equal(ApplicationStatus.Submitting, saved.Draft.Status);
        Assert.NotNull(saved.Submission!.UsedAt);
    }

    [Fact]
    public async Task PostSubmitCrash_IsUnverifiedNotRetryable()
    {
        var record = Ready();
        var store = await Store();
        await store.CreateAsync(record);
        await store.ClaimSubmissionAsync(record.Draft.Id, Now);
        var restarted = await Store();
        await restarted.RecoverInterruptedAsync();
        var recovered = await restarted.GetAsync(record.Draft.Id);
        Assert.NotNull(recovered);
        Assert.Equal(ApplicationStatus.SubmittedUnverified, recovered.Draft.Status);
        Assert.NotNull(recovered.Submission);
        Assert.Equal(record.Submission!.Id, recovered.Submission.Id);
        Assert.NotNull(recovered.Submission.UsedAt);
        Assert.False(await restarted.ClaimSubmissionAsync(record.Draft.Id, Now));
    }

    [Fact]
    public async Task NoApproval_CannotClaimSubmission()
    {
        var record = Ready() with { Submission = null };
        var store = await Store();
        await store.CreateAsync(record);
        Assert.False(await store.ClaimSubmissionAsync(record.Draft.Id, Now));
        var saved = await store.GetAsync(record.Draft.Id);
        Assert.NotNull(saved);
        Assert.Equal(ApplicationStatus.AwaitingSubmissionApproval, saved.Draft.Status);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
