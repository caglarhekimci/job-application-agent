using System.Text.Json;
using JobAgent.Core;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Profiles;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace JobAgent.E2E.Tests;

public sealed class ResumableSyntheticTests : IDisposable
{
    private readonly List<string> roots = [];

    [Fact]
    public async Task ReviewedScopedAnswers_ResumeSameApplicationAndInvalidatePreparedApprovals()
    {
        var root = TemporaryDirectory();
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
        await site.StartAsync();
        Guid applicationRef;

        await using (var first = NewWorkflow(root, site.Urls.Single()))
        {
            await first.LoadFixtureAsync();
            await first.ConfirmProfileFromUiAsync();
            var paused = await first.CreateSyntheticDraftForHostAsync();
            applicationRef = paused.ApplicationRef;

            Assert.Equal(ApplicationStatus.NeedsInput, paused.Status);
            Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
            var initialState = StateApplication(await first.GetStateAsync());
            Assert.Equal(applicationRef, initialState.GetProperty("draft").GetProperty("Id").GetGuid());
            Assert.Equal(7, initialState.GetProperty("questionReview").GetProperty("Questions").GetArrayLength());
        }

        await using var workflow = NewWorkflow(root, site.Urls.Single());
        var restarted = await workflow.GetHostStatusAsync(applicationRef);
        Assert.Equal(ApplicationStatus.NeedsInput, restarted.Status);
        var beforeReview = StateApplication(await workflow.GetStateAsync());
        var reviewed = await workflow.ReviewAnswersFromUiAsync(applicationRef,
            beforeReview.GetProperty("payloadHash").GetString()!,
            [
                Reviewed("preference.work.mode", "remote"),
                Reviewed("preference.travel", "false"),
                Reviewed("preference.contact.method", "email")
            ]);

        Assert.Equal(applicationRef, reviewed.ApplicationRef);
        Assert.Equal(ApplicationStatus.ReadyForDataSharing, reviewed.Status);
        var savedProfile = await OpenProfiles(root).GetLatestAsync(SyntheticData.Profile().Id);
        Assert.NotNull(savedProfile);
        Assert.Equal(3, savedProfile.Answers.Count);
        Assert.All(savedProfile.Answers, answer =>
        {
            Assert.Equal(AnswerScopeType.Application, answer.Scope);
            Assert.Equal(applicationRef.ToString("D"), answer.ScopeId);
        });

        await workflow.RequestHostReviewAsync(applicationRef);
        await workflow.ShareAndFillFromUiAsync("trusted-ui-session");
        var approved = await workflow.ApproveForHostFromUiAsync(applicationRef, "trusted-ui-session");
        Assert.True(approved.SubmissionApproved);
        var preparedState = StateApplication(await workflow.GetStateAsync());
        var preparedHash = preparedState.GetProperty("payloadHash").GetString()!;

        var changed = await workflow.ReviewAnswersFromUiAsync(applicationRef, preparedHash,
            [Reviewed("preference.work.mode", "hybrid")]);

        Assert.Equal(applicationRef, changed.ApplicationRef);
        Assert.Equal(ApplicationStatus.ReadyForDataSharing, changed.Status);
        Assert.NotEqual(preparedHash, changed.PayloadHash);
        var reset = await new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"))
            .GetAsync(applicationRef);
        Assert.NotNull(reset);
        Assert.Null(reset.Sharing);
        Assert.Null(reset.Submission);
        Assert.Null(reset.HostReviewRequestedAt);
        var noOldExecution = await Assert.ThrowsAsync<PolicyException>(() =>
            workflow.ExecuteAlreadyApprovedAsync(applicationRef));
        Assert.Equal("SubmissionNotReady", noOldExecution.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);

        await workflow.ShareAndFillFromUiAsync("fresh-ui-session");
        await workflow.SubmitFromUiAsync("fresh-ui-session");

        var completed = await workflow.GetHostStatusAsync(applicationRef);
        Assert.Equal(ApplicationStatus.SubmittedVerified, completed.Status);
        var receipt = Assert.Single(site.Services.GetRequiredService<ReceiptStore>().Receipts);
        Assert.Equal("hybrid", receipt.WorkMode);
        Assert.False(receipt.Travel);
        Assert.Equal("email", receipt.ContactMethod);
        Assert.Equal(1, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    private static DemoWorkflow NewWorkflow(string root, string origin) =>
        new(root, origin, HappyPathTests.FindRepo(), extendedControls: true);

    private static ReviewedAnswerMemoryUpdate Reviewed(string key, string answer) => new()
    {
        SemanticKey = key,
        Language = "en",
        Answer = answer,
        Scope = AnswerScopeType.Application
    };

    private static JsonElement StateApplication(object state) =>
        JsonSerializer.SerializeToElement(state).GetProperty("application");

    private static ProfileRepository OpenProfiles(string root) => new(new ProfileStoreOptions
    {
        DatabasePath = Path.Combine(root, "profiles.db"),
        CheckoutRoot = HappyPathTests.FindRepo(),
        AllowSyntheticPlaintextForLinuxTests = !OperatingSystem.IsWindows()
    }, OperatingSystem.IsWindows() ? new WindowsDpapiPayloadProtector() : new SyntheticPlaintextPayloadProtector());

    private string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "jobagent-resumable-" + Guid.NewGuid().ToString("N"));
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
