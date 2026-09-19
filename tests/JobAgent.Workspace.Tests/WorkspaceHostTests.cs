using System.Text;
using System.Text.Json;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Xunit;

namespace JobAgent.Workspace.Tests;

public sealed class WorkspaceHostTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-host-workspace-" + Guid.NewGuid());
    private LocalWorkspace Open() => new(root, Environment.CurrentDirectory, new WindowsDpapiPayloadProtector());

    private static async Task<WorkspaceView> Setup(LocalWorkspace workspace)
    {
        var imported = await workspace.ImportAsync(new MemoryStream("Synthetic User\nProfessional C# developer 2020-2024"u8.ToArray()), "synthetic.txt");
        var reviewed = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = imported.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid",
            SalaryTarget = 100000,
            SalaryPrivateMinimum = 85000,
            Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1),
                Role = "Developer", Skills = ["C#"] }]
        });
        return await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = reviewed.Revision,
            Employer = "Synthetic Employer",
            Title = "Developer",
            Text = "Build local C# tools.",
            SourceUrl = "https://example.invalid/synthetic"
        });
    }

    [Fact]
    public async Task HostAndUiShareProtectedReferencesAndPersistMissingQuestions()
    {
        Guid applicationRef;
        using (var workspace = Open())
        {
            var original = await Setup(workspace);
            var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
            var refs = await host.GetHostWorkspaceRefsAsync();
            Assert.Equal(original.Profile.Id, refs.ProfileRef);
            Assert.NotNull(refs.ResumeRef);
            Assert.Equal(original.Revision, (await workspace.GetAsync()).Revision);
            var draft = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
            applicationRef = draft.ApplicationRef;
            Assert.Equal(ApplicationStatus.NeedsInput, draft.Status);
            Assert.False(draft.SubmissionApproved);
            Assert.Contains((await host.GetApplicationQuestionsAsync(applicationRef)).Questions, q => q.Key == "motivation");
            Assert.Contains(applicationRef.ToString(), await workspace.ExportAsync());
            Assert.Equal(applicationRef, (await host.CreateApplicationAsync(new(refs.JobRef.Value, refs.ProfileRef.Value, refs.ResumeRef.Value))).ApplicationRef);
        }
        using var reopened = Open();
        Assert.Equal(ApplicationStatus.NeedsInput,
            (await Assert.IsAssignableFrom<ILocalWorkspaceHost>(reopened).GetApplicationStatusAsync(applicationRef)).Status);
        foreach (var file in Directory.GetFiles(root))
            Assert.DoesNotContain("Synthetic User", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)));
    }

    [Fact]
    public async Task ProfileProposalNeverChangesVerifiedProfileAndSummaryOmitsPrivateData()
    {
        using var workspace = Open();
        var before = await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var summary = JsonSerializer.Serialize(await host.GetHostProfileSummaryAsync(before.Profile.Id));
        Assert.DoesNotContain("Synthetic User", summary);
        Assert.DoesNotContain("synthetic@example.invalid", summary);
        Assert.DoesNotContain("85000", summary);
        var proposal = await host.ProposeProfilePatchAsync(new(before.Profile.Id, before.Profile.Version,
            [new(HostProfileField.FullName, "Proposed name", [])]));
        Assert.True(proposal.RequiresUserReview);
        Assert.Equal("Pending", proposal.Status);
        Assert.Equal(before.Profile.FullName, (await workspace.GetAsync()).Profile.FullName);
        Assert.Equal(before.Profile.Version, (await workspace.GetAsync()).Profile.Version);
        await Assert.ThrowsAsync<PolicyException>(() => host.ProposeProfilePatchAsync(new(before.Profile.Id,
            before.Profile.Version - 1, [new(HostProfileField.Locale, "tr", [])])));
        await Assert.ThrowsAsync<ArgumentException>(() => host.ProposeProfilePatchAsync(new(before.Profile.Id,
            before.Profile.Version, [new((HostProfileField)999, "bad", [])])));
    }

    [Fact]
    public async Task ImportedJobRemainsAProposalAndUnknownReferencesCannotOpenAnotherWorkspace()
    {
        using var workspace = Open();
        var before = await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var job = await host.ImportJobProposalAsync(new("We need professional C# experience.",
            "https://example.invalid/never-fetch", "Proposal employer", "Proposal role"));
        Assert.True(job.RequiresUserReview);
        Assert.Equal(before.Job!.Id, (await workspace.GetAsync()).Job!.Id);
        var evaluation = await host.EvaluateJobAsync(job.JobRef, before.Profile.Id);
        Assert.NotEqual(JobAgent.Core.Jobs.JobEvaluationStatus.Eligible, evaluation.Evaluation.Status);
        await Assert.ThrowsAsync<PolicyException>(() => host.GetHostProfileSummaryAsync(Guid.NewGuid()));
        var refs = await host.GetHostWorkspaceRefsAsync();
        await Assert.ThrowsAsync<PolicyException>(() => host.CreateApplicationAsync(new(job.JobRef, before.Profile.Id, refs.ResumeRef!.Value)));
    }

    [Fact]
    public async Task ModelAnswerProposalsStayUnapprovedAndEnforceApplicationRevision()
    {
        using var workspace = Open();
        await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var refs = await host.GetHostWorkspaceRefsAsync();
        var draft = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
        var evidence = Assert.Single((await host.GetHostProfileSummaryAsync(refs.ProfileRef.Value)).Evidence).EvidenceRef;
        var request = new HostAnswerProposalRequest(draft.ApplicationRef, draft.Revision,
            [new(1, "motivation", "I want to work on C# tools.", "tr", [evidence], "Needs user review.")], [evidence]);
        var result = await host.ProposeApplicationAnswersAsync(request);
        Assert.Equal(AnswerProposalDisposition.RequiresReview, Assert.Single(result.Results).Disposition);
        Assert.True(result.RequiresUserReview);
        Assert.Empty((await workspace.GetAsync()).Profile.Answers);
        Assert.False((await host.GetApplicationStatusAsync(draft.ApplicationRef)).SubmissionApproved);
        await Assert.ThrowsAsync<PolicyException>(() => host.ProposeApplicationAnswersAsync(request));
        var invalid = request with { BaseRevision = result.Revision, EvidenceRefs = ["invented"] };
        Assert.Equal(AnswerProposalDisposition.Abstained,
            Assert.Single((await host.ProposeApplicationAnswersAsync(invalid)).Results).Disposition);
    }

    [Fact]
    public async Task CancelPersistsAndNoPersonalHostOperationCanExecuteAnEmployerSubmission()
    {
        using var workspace = Open();
        await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var refs = await host.GetHostWorkspaceRefsAsync();
        var draft = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
        var denied = await Assert.ThrowsAsync<PolicyException>(() => host.ExecuteApprovedApplicationAsync(draft.ApplicationRef));
        Assert.Equal("BlockedPermission", denied.Code);
        var cancelled = await host.CancelApplicationAsync(draft.ApplicationRef);
        Assert.Equal(ApplicationStatus.Cancelled, cancelled.Status);
        await Assert.ThrowsAsync<PolicyException>(() => host.PrepareApplicationReviewAsync(draft.ApplicationRef));
        Assert.Equal(ApplicationStatus.Cancelled, (await host.CancelApplicationAsync(draft.ApplicationRef)).Status);
    }

    [Fact]
    public async Task ResumeReferenceChangesOnImportAndStaleReferencesAreRejected()
    {
        using var workspace = Open();
        await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var before = await host.GetHostWorkspaceRefsAsync();
        await workspace.ImportAsync(new MemoryStream("Changed synthetic CV"u8.ToArray()), "changed.txt");
        var after = await host.GetHostWorkspaceRefsAsync();
        Assert.NotEqual(before.ResumeRef, after.ResumeRef);
        await Assert.ThrowsAsync<PolicyException>(() => host.CreateApplicationAsync(new(before.JobRef!.Value,
            before.ProfileRef!.Value, before.ResumeRef!.Value)));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
