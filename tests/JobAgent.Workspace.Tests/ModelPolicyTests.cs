using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Models;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Xunit;

namespace JobAgent.Workspace.Tests;

public sealed class ModelPolicyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-model-policy-" + Guid.NewGuid());
    private LocalWorkspace Open() => new(root, Environment.CurrentDirectory, new WindowsDpapiPayloadProtector());

    [Fact]
    public async Task ExactBudgetPersistsAcrossRestartAndAllAbstainCounts()
    {
        Guid applicationRef;
        using (var workspace = Open())
        {
            await Setup(workspace);
            var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
            var refs = await host.GetHostWorkspaceRefsAsync();
            var created = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
            applicationRef = created.ApplicationRef;
            var configured = await workspace.UpdateModelPolicyFromUiAsync(new(created.Revision,
                ModelProviderMode.HostMediated, 2));

            var first = await host.ProposeApplicationAnswersAsync(Request(applicationRef, configured.Revision, "invented"));
            Assert.Equal(AnswerProposalDisposition.Abstained, Assert.Single(first.Results).Disposition);
            var second = await host.ProposeApplicationAnswersAsync(Request(applicationRef, first.Revision, "invented"));
            Assert.Equal(AnswerProposalDisposition.Abstained, Assert.Single(second.Results).Disposition);
            Assert.Equal(2, Assert.Single((await workspace.GetModelPolicyAsync()).Applications).UsedOperations);
        }

        using var reopened = Open();
        var current = await reopened.GetModelPolicyAsync();
        var error = await Assert.ThrowsAsync<PolicyException>(() =>
            Assert.IsAssignableFrom<ILocalWorkspaceHost>(reopened)
                .ProposeApplicationAnswersAsync(Request(applicationRef, current.Revision, "invented")));
        Assert.Equal("BudgetExceeded", error.Code);
        Assert.Equal(2, Assert.Single((await reopened.GetModelPolicyAsync()).Applications).UsedOperations);
    }

    [Fact]
    public async Task ConcurrentRequestsCannotExceedLimitAndPolicyUpdatesDoNotResetUsage()
    {
        using var workspace = Open();
        await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var refs = await host.GetHostWorkspaceRefsAsync();
        var created = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
        var configured = await workspace.UpdateModelPolicyFromUiAsync(new(created.Revision,
            ModelProviderMode.HostMediated, 1));

        var attempts = await Task.WhenAll(
            Attempt(() => host.ProposeApplicationAnswersAsync(Request(created.ApplicationRef, configured.Revision, "invented"))),
            Attempt(() => host.ProposeApplicationAnswersAsync(Request(created.ApplicationRef, configured.Revision, "invented"))));

        Assert.Single(attempts, result => result is null);
        Assert.Single(attempts, result => result is PolicyException);
        var view = await workspace.GetModelPolicyAsync();
        Assert.Equal(1, Assert.Single(view.Applications).UsedOperations);
        var expanded = await workspace.UpdateModelPolicyFromUiAsync(new(view.Revision,
            ModelProviderMode.HostMediated, 2));
        Assert.Equal(1, Assert.Single(expanded.Applications).UsedOperations);
        var lowered = await workspace.UpdateModelPolicyFromUiAsync(new(expanded.Revision,
            ModelProviderMode.HostMediated, 1));
        Assert.Equal(1, Assert.Single(lowered.Applications).UsedOperations);
        Assert.Equal("BudgetExceeded", (await Assert.ThrowsAsync<PolicyException>(() =>
            host.ProposeApplicationAnswersAsync(Request(created.ApplicationRef, lowered.Revision, "invented")))).Code);
    }

    [Fact]
    public async Task FixtureBlocksEveryHostProposalWriteButManualReviewStillWorks()
    {
        using var workspace = Open();
        await Setup(workspace);
        var host = Assert.IsAssignableFrom<ILocalWorkspaceHost>(workspace);
        var refs = await host.GetHostWorkspaceRefsAsync();
        var created = await host.CreateApplicationAsync(new(refs.JobRef!.Value, refs.ProfileRef!.Value, refs.ResumeRef!.Value));
        var fixture = await workspace.UpdateModelPolicyFromUiAsync(new(created.Revision, ModelProviderMode.Fixture, 4));
        var policyRefs = await host.GetHostWorkspaceRefsAsync();
        Assert.Equal(ModelProviderMode.Fixture, policyRefs.ProviderMode);
        Assert.False(policyRefs.PaidApiEnabled);
        Assert.Equal(4, policyRefs.MaxAnswerProposalOperationsPerApplication);

        Assert.Equal("ProviderModeDisabled", (await Assert.ThrowsAsync<PolicyException>(() =>
            host.ProposeProfilePatchAsync(new(refs.ProfileRef.Value, refs.ProfileVersion!.Value,
                [new(HostProfileField.FullName, "Proposal", [])])))).Code);
        Assert.Equal("ProviderModeDisabled", (await Assert.ThrowsAsync<PolicyException>(() =>
            host.ImportJobProposalAsync(new("A supplied local job description.")))).Code);
        Assert.Equal("ProviderModeDisabled", (await Assert.ThrowsAsync<PolicyException>(() =>
            host.ProposeApplicationAnswersAsync(Request(created.ApplicationRef, fixture.Revision, "invented")))).Code);

        var panel = await workspace.GetApplicationPanelAsync();
        var application = Assert.Single(panel.Applications);
        var reviewed = await workspace.ReviewApplicationAnswersFromUiAsync(new(application.Draft.Id, panel.Revision,
            application.Draft.PayloadHash(), [new()
            {
                SemanticKey = "motivation",
                Answer = "User supplied answer",
                Language = "tr",
                Scope = AnswerScopeType.Application
            }]));
        Assert.Equal("User supplied answer", reviewed.Draft.Answers["motivation"]);
        Assert.Empty((await workspace.GetModelPolicyAsync()).Applications);
    }

    [Fact]
    public async Task InvalidUiPolicyUpdatesFailWithoutMutation()
    {
        using var workspace = Open();
        await Setup(workspace);
        var before = await workspace.GetModelPolicyAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() => workspace.UpdateModelPolicyFromUiAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.UpdateModelPolicyFromUiAsync(new(before.Revision,
            (ModelProviderMode)999, 4)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => workspace.UpdateModelPolicyFromUiAsync(new(before.Revision,
            ModelProviderMode.HostMediated, 0)));
        var paid = await Assert.ThrowsAsync<PolicyException>(() => workspace.UpdateModelPolicyFromUiAsync(new(before.Revision,
            ModelProviderMode.Api, 4)));

        Assert.Equal("PaidApiDisabled", paid.Code);
        var after = await workspace.GetModelPolicyAsync();
        Assert.Equal(before, after);
    }

    private static async Task Setup(LocalWorkspace workspace)
    {
        var imported = await workspace.ImportAsync(new MemoryStream("Synthetic User\nProfessional C# developer 2020-2024"u8.ToArray()), "synthetic.txt");
        var reviewed = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = imported.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid",
            Experience = [new()
            {
                SourceSpan = "line 2",
                Start = new(2020, 1, 1),
                End = new(2024, 1, 1),
                Role = "Developer",
                Kind = ExperienceKind.Professional,
                Skills = ["C#"]
            }]
        });
        await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = reviewed.Revision,
            Employer = "Synthetic Employer",
            Title = "Developer",
            Text = "Build local C# tools.",
            SourceUrl = "https://example.invalid/synthetic"
        });
    }

    private static HostAnswerProposalRequest Request(Guid applicationRef, long revision, string evidence) => new(
        applicationRef, revision,
        [new(1, "motivation", "Proposed answer", "tr", [evidence], "Requires review.")],
        [evidence]);

    private static async Task<Exception?> Attempt(Func<Task<HostAnswerProposalResult>> action)
    {
        try { await action(); return null; }
        catch (Exception error) { return error; }
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
