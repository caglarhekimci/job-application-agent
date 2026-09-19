using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Xunit;

namespace JobAgent.Workspace.Tests;

public sealed class WorkspaceApplicationReviewTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-application-review-" + Guid.NewGuid());
    private LocalWorkspace Open() => new(root, Environment.CurrentDirectory, new WindowsDpapiPayloadProtector());
    private static readonly FormQuestion Question = new() { Key = "availability", Label = "Ne zaman başlayabilirsiniz?", Language = "tr" };
    private static async Task Setup(LocalWorkspace workspace)
    {
        var imported = await workspace.ImportAsync(new MemoryStream("Synthetic User\nProfessional C# developer 2020-2024"u8.ToArray()), "synthetic.txt");
        var profile = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = imported.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid",
            Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1),
                Role = "Developer", Kind = ExperienceKind.Professional, Skills = ["C#"] }]
        });
        await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = profile.Revision,
            Employer = "Synthetic Employer",
            Title = "Developer",
            Text = "Build local tools.",
            SourceUrl = "https://example.invalid/job"
        });
    }

    [Fact]
    public async Task NewQuestionPausesAndRemembersScopedAnswerForSameProtectedApplication()
    {
        Guid id;
        using (var workspace = Open())
        {
            await Setup(workspace);
            var created = await workspace.CreateApplicationFromUiAsync(new((await workspace.GetAsync()).Revision, [Question], "dotnet-developer"));
            id = created.Draft.Id;
            Assert.Equal(ApplicationStatus.NeedsInput, created.Draft.Status);
            var resumed = await workspace.ReviewApplicationAnswersFromUiAsync(new(id, created.WorkspaceRevision,
                created.Draft.PayloadHash(), [new() { SemanticKey = Question.Key, Answer = "İki hafta içinde", Language = "tr", Scope = AnswerScopeType.Application }]));
            Assert.Equal(id, resumed.Draft.Id);
            Assert.Equal(ApplicationStatus.ReadyForDataSharing, resumed.Draft.Status);
            Assert.Equal(id.ToString("D"), Assert.Single((await workspace.GetAsync()).Profile.Answers).ScopeId);
        }
        using var reopened = Open();
        var stored = Assert.Single((await reopened.GetApplicationPanelAsync()).Applications);
        Assert.Equal(id, stored.Draft.Id);
        Assert.Equal("İki hafta içinde", stored.Draft.Answers[Question.Key]);
    }

    [Fact]
    public async Task ChangedQuestionsClearReviewReferenceAndStaleReviewCannotBeReused()
    {
        using var workspace = Open();
        await Setup(workspace);
        var created = await workspace.CreateApplicationFromUiAsync(new((await workspace.GetAsync()).Revision, [Question], "dotnet-developer"));
        var reviewed = await workspace.ReviewApplicationAnswersFromUiAsync(new(created.Draft.Id, created.WorkspaceRevision,
            created.Draft.PayloadHash(), [new() { SemanticKey = Question.Key, Answer = "İki hafta", Language = "tr", Scope = AnswerScopeType.RoleGroup }]));
        var prepared = await workspace.PrepareApplicationReviewAsync(created.Draft.Id);
        Assert.True(prepared.ReviewRequested);
        var changed = await workspace.UpdateApplicationQuestionsFromUiAsync(new(created.Draft.Id, prepared.Revision,
            [Question with { Label = "Başlangıç tarihi?", MaxLength = 20 }], "dotnet-developer"));
        Assert.False(changed.ReviewRequested);
        Assert.NotEqual(reviewed.Draft.PayloadHash(), changed.Draft.PayloadHash());
        await Assert.ThrowsAsync<PolicyException>(() => workspace.ReviewApplicationAnswersFromUiAsync(new(created.Draft.Id,
            changed.WorkspaceRevision, reviewed.Draft.PayloadHash(),
            [new() { SemanticKey = Question.Key, Answer = "Bir ay", Language = "tr", Scope = AnswerScopeType.RoleGroup }])));
    }

    [Fact]
    public async Task SourceReplacementPausesDraftAndRequiresExplicitRebindAfterProfileReview()
    {
        using var workspace = Open();
        await Setup(workspace);
        var created = await workspace.CreateApplicationFromUiAsync(new((await workspace.GetAsync()).Revision,
            [new() { Key = "contact.name", Label = "Ad soyad", Language = "tr" }], ""));
        var oldHash = created.Draft.ResumeHash;
        await workspace.PrepareApplicationReviewAsync(created.Draft.Id);
        var imported = await workspace.ImportAsync(new MemoryStream("New synthetic source"u8.ToArray()), "new.txt");
        Assert.Equal(ApplicationStatus.NeedsInput, (await workspace.GetApplicationStatusAsync(created.Draft.Id)).Status);
        await Assert.ThrowsAsync<PolicyException>(() => workspace.RefreshApplicationSourcesFromUiAsync(created.Draft.Id, imported.Revision));
        var profile = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = imported.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid"
        });
        var rebound = await workspace.RefreshApplicationSourcesFromUiAsync(created.Draft.Id, profile.Revision);
        Assert.NotEqual(oldHash, rebound.Draft.ResumeHash);
        Assert.Equal(created.Draft.Id, rebound.Draft.Id);
        Assert.False(rebound.ReviewRequested);
        Assert.Equal(ApplicationStatus.ReadyForDataSharing, rebound.Draft.Status);
    }

    [Fact]
    public async Task UserCanReviewImportedJobWithoutHostMarkingItReviewed()
    {
        using var workspace = Open();
        await Setup(workspace);
        var proposed = await workspace.ImportJobProposalAsync(new("Build C# tools.", null, "New employer", "New role"));
        var approved = await workspace.ReviewImportedJobFromUiAsync(proposed.JobRef, new()
        {
            ExpectedRevision = proposed.Revision,
            Employer = "Reviewed employer",
            Title = "Reviewed role",
            Text = "Build C# tools."
        });
        Assert.Equal(proposed.JobRef, Guid.Parse(approved.Job!.Id));
        Assert.NotNull(approved.Job.RequirementsReviewedAt);
        Assert.Empty((await workspace.GetApplicationPanelAsync()).JobProposals);
    }

    [Fact]
    public async Task ExpiredSourceProposalIsFilteredAndAcceptanceRemovesCaseInsensitively()
    {
        using var workspace = Open();
        await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var refs = await host.GetHostWorkspaceRefsAsync();
        var created = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
        var evidence = Assert.Single((await host.GetHostProfileSummaryAsync(refs.ProfileRef.Value)).Evidence).EvidenceRef;
        var proposed = await host.ProposeApplicationAnswersAsync(new(created.ApplicationRef, created.Revision,
            [new(1, "motivation", "I want to build local tools.", "tr", [evidence], "Grounded in supplied experience.")], [evidence]));
        Assert.Single(Assert.Single((await workspace.GetApplicationPanelAsync()).Applications).Proposals);

        var reviewed = await workspace.ReviewApplicationAnswersFromUiAsync(new(created.ApplicationRef, proposed.Revision,
            (await workspace.GetApplicationPanelAsync()).Applications.Single().PayloadHash,
            [new() { SemanticKey = "MOTIVATION", Language = "tr", Answer = "Reviewed answer",
                Scope = AnswerScopeType.Application, EvidenceIds = [evidence] }]));
        Assert.Empty(reviewed.Proposals);

        var nextProposal = await host.ProposeApplicationAnswersAsync(new(created.ApplicationRef, reviewed.WorkspaceRevision,
            [new(1, "motivation", "Another proposal.", "tr", [evidence], "Grounded in supplied experience.")], [evidence]));
        var profile = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = nextProposal.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid",
            Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1),
                Role = "Developer", Kind = ExperienceKind.Professional, Skills = ["C#"] }]
        });
        Assert.Empty(Assert.Single((await workspace.GetApplicationPanelAsync()).Applications).Proposals);
        Assert.True(profile.Profile.Version > refs.ProfileVersion);
    }

    [Fact]
    public async Task JobProposalRetryIsIdempotentAndPendingProposalsCanBeDiscarded()
    {
        using var workspace = Open();
        await Setup(workspace);
        var request = new HostJobImportRequest("Build C# tools.", "https://example.invalid/jobs/1?utm_source=test",
            "New employer", "New role");
        var first = await workspace.ImportJobProposalAsync(request);
        var duplicate = await workspace.ImportJobProposalAsync(request with
        { SourceUrl = "https://example.invalid/jobs/1?utm_source=retry" });
        Assert.Equal(first.JobRef, duplicate.JobRef);
        Assert.Equal(first.Revision, duplicate.Revision);
        Assert.Single((await workspace.GetApplicationPanelAsync()).JobProposals);

        var distinct = await workspace.ImportJobProposalAsync(request with { Title = "Different role" });
        Assert.NotEqual(first.JobRef, distinct.JobRef);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workspace.DiscardProposalAsync(distinct.JobRef, first.Revision));
        var afterJobDiscard = await workspace.DiscardProposalAsync(distinct.JobRef, distinct.Revision);
        Assert.Single(afterJobDiscard.JobProposals);

        var state = await workspace.GetAsync();
        var profileProposal = await workspace.ProposeProfilePatchAsync(new(state.Profile.Id, state.Profile.Version,
            [new(HostProfileField.FullName, "Proposed User", [])]));
        var afterProfileDiscard = await workspace.DiscardProposalAsync(profileProposal.ProposalRef, profileProposal.Revision);
        Assert.Empty(afterProfileDiscard.ProfileProposals);
    }

    [Fact]
    public async Task UiCancellationRejectsStaleRevisionAndNullPayloadMembersAreBounded()
    {
        using var workspace = Open();
        await Setup(workspace);
        var created = await workspace.CreateApplicationFromUiAsync(new((await workspace.GetAsync()).Revision,
            [Question], "dotnet-developer"));
        var changed = await workspace.UpdateApplicationQuestionsFromUiAsync(new(created.Draft.Id, created.WorkspaceRevision,
            [Question with { Label = "Başlangıç zamanı" }], "dotnet-developer"));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.ReviewApplicationAnswersFromUiAsync(new(
            created.Draft.Id, changed.WorkspaceRevision, changed.Draft.PayloadHash(),
            [new() { SemanticKey = Question.Key, Language = Question.Language, Answer = null! }])));

        var proposal = await workspace.ImportJobProposalAsync(new("Text", null, "Employer", "Role"));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.ReviewImportedJobFromUiAsync(proposal.JobRef, new()
        { ExpectedRevision = proposal.Revision, Employer = "Employer", Title = "Role", Text = "Text", SourceUrl = null! }));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.ReviewImportedJobFromUiAsync(proposal.JobRef, new()
        { ExpectedRevision = proposal.Revision, Employer = "Employer", Title = "Role", Text = "Text", Requirements = null! }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workspace.CancelApplicationFromUiAsync(created.Draft.Id, created.WorkspaceRevision));
        Assert.Equal(ApplicationStatus.Cancelled,
            (await workspace.CancelApplicationFromUiAsync(created.Draft.Id, proposal.Revision)).Draft.Status);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
