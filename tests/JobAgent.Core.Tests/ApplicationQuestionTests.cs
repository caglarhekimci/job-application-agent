using JobAgent.Core;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class ApplicationQuestionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
    private static readonly FormQuestion Preference = new()
    { Key = "preference.work.mode", Label = "Work arrangement", Language = "en", MaxLength = 30 };

    [Fact]
    public void MissingQuestionCreatesReviewSnapshotAndResumesSameApplicationAfterScopedAnswer()
    {
        var profile = SyntheticData.Profile();
        var job = SyntheticData.Job() with { RoleGroupId = "dotnet-developer" };
        var draft = new ApplicationDraft { ProfileId = profile.Id, JobKey = job.Id, Synthetic = true };
        var paused = ApplicationQuestions.Resolve(draft, [Preference], profile, job, Now);
        Assert.Equal(ApplicationStatus.NeedsInput, paused.Draft.Status);
        Assert.Equal(AnswerStatus.NeedsInput, Assert.Single(paused.Questions).Answer.Status);
        Assert.Empty(paused.Draft.Answers);
        Assert.Equal(Preference, Assert.Single(paused.Draft.Questions));

        var reviewed = ApplicationQuestions.RememberReviewed(paused.Draft, profile, job,
            new ReviewedAnswerMemoryUpdate
            {
                SemanticKey = Preference.Key,
                Language = "en",
                Answer = "remote",
                Scope = AnswerScopeType.Application
            }, Now);
        var resumed = ApplicationQuestions.Resolve(paused.Draft, paused.Draft.Questions, reviewed, job, Now);
        Assert.Equal(draft.Id, resumed.Draft.Id);
        Assert.Equal(ApplicationStatus.ReadyForDataSharing, resumed.Draft.Status);
        Assert.Equal("remote", resumed.Draft.Answers[Preference.Key]);
        Assert.Equal(draft.Id.ToString("D"), Assert.Single(reviewed.Answers).ScopeId);
        var other = draft with { Id = Guid.NewGuid() };
        Assert.Equal(ApplicationStatus.NeedsInput,
            ApplicationQuestions.Resolve(other, [Preference], reviewed, job, Now).Draft.Status);
    }

    [Fact]
    public void RoleGroupMemoryUsesStableIdAndExplicitScopePrecedence()
    {
        var job = SyntheticData.Job() with { RoleGroupId = "dotnet-developer" };
        var applicationId = Guid.NewGuid();
        var profile = SyntheticData.Profile() with
        {
            Answers = [
            new() { SemanticKey = Preference.Key, Language = "en", Answer = "global", Scope = AnswerScopeType.Default },
            new() { SemanticKey = Preference.Key, Language = "en", Answer = "role", Scope = AnswerScopeType.RoleGroup, ScopeId = job.RoleGroupId },
            new() { SemanticKey = Preference.Key, Language = "en", Answer = "company", Scope = AnswerScopeType.Company, ScopeId = job.Employer },
            new() { SemanticKey = Preference.Key, Language = "en", Answer = "application", Scope = AnswerScopeType.Application, ScopeId = applicationId.ToString("D") }
        ]
        };
        var context = new AnswerScopeContext(applicationId, job.RoleGroupId);
        Assert.Equal("application", AnswerResolver.Resolve(Preference, profile, job, Now, context).Value);
        Assert.Equal("company", AnswerResolver.Resolve(Preference, profile, job, Now,
            context with { ApplicationId = Guid.NewGuid() }).Value);
        Assert.Equal("role", AnswerResolver.Resolve(Preference, profile, job with { Employer = "Another" }, Now,
            context with { ApplicationId = Guid.NewGuid() }).Value);
        Assert.Equal("global", AnswerResolver.Resolve(Preference, profile, job with { Employer = "Another" }, Now,
            new(Guid.NewGuid(), "different-role")).Value);
    }

    [Fact]
    public void ChangedQuestionInvalidatesApprovalEvenWhenResolvedValueIsIdentical()
    {
        var profile = TestProfiles.Synthetic();
        var job = SyntheticData.Job();
        var question = new FormQuestion { Key = "contact.name", Label = "Name", Language = "en" };
        var draft = ApplicationQuestions.Resolve(new() { ProfileId = profile.Id, JobKey = job.Id },
            [question], profile, job, Now).Draft;
        var approval = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.ShareData, "ui", Now);
        var changed = ApplicationQuestions.Resolve(draft, [question with { Label = "Your full legal name" }],
            profile, job, Now).Draft;
        Assert.Equal(draft.Answers[question.Key], changed.Answers[question.Key]);
        Assert.NotEqual(draft.PayloadHash(), changed.PayloadHash());
        Assert.Throws<PolicyException>(() => ApprovalPolicy.Validate(changed, approval,
            ApprovalPurpose.ShareData, Now));
    }

    [Fact]
    public void SensitiveQuestionsCannotBeTurnedIntoAutomatableMemory()
    {
        var profile = SyntheticData.Profile();
        var job = SyntheticData.Job();
        var draft = ApplicationQuestions.Resolve(new() { ProfileId = profile.Id, JobKey = job.Id },
            [Preference with { Sensitive = true }], profile, job, Now).Draft;
        Assert.Equal(ApplicationStatus.NeedsInput, draft.Status);
        Assert.Throws<PolicyException>(() => ApplicationQuestions.RememberReviewed(draft, profile, job,
            new() { SemanticKey = Preference.Key, Language = "en", Answer = "value" }, Now));
    }

    [Fact]
    public void InvalidOrAmbiguousQuestionSetsAndTerminalMutationsAreRejected()
    {
        var profile = SyntheticData.Profile();
        var job = SyntheticData.Job();
        var draft = new ApplicationDraft { ProfileId = profile.Id, JobKey = job.Id };
        Assert.Throws<ArgumentException>(() => ApplicationQuestions.Resolve(draft,
            [Preference, Preference with { Key = "PREFERENCE.WORK.MODE" }], profile, job, Now));
        Assert.Throws<ArgumentException>(() => ApplicationQuestions.Resolve(draft,
            [Preference with { Key = " " }], profile, job, Now));
        Assert.Throws<PolicyException>(() => ApplicationQuestions.Resolve(draft with { Status = ApplicationStatus.Cancelled },
            [Preference], profile, job, Now));
        Assert.Throws<PolicyException>(() => ApplicationQuestions.Resolve(draft with { ProfileId = Guid.NewGuid() },
            [Preference], profile, job, Now));
        Assert.Throws<ArgumentException>(() => ApplicationQuestions.Resolve(draft,
            [null!], profile, job, Now));

        var paused = ApplicationQuestions.Resolve(draft, [Preference], profile, job, Now).Draft;
        var nullAnswer = new ReviewedAnswerMemoryUpdate
        {
            SemanticKey = Preference.Key,
            Language = Preference.Language,
            Answer = null!
        };
        Assert.Throws<ArgumentException>(() =>
            ApplicationQuestions.RememberReviewed(paused, profile, job, nullAnswer, Now));
    }
}
