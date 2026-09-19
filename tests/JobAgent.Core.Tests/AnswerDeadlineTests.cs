using System.Text.Json;
using JobAgent.Core;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class AnswerDeadlineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-20T00:00:00Z");
    private static readonly FormQuestion Preference = new() { Key = "preference.work.mode", Label = "Work mode" };

    [Theory]
    [InlineData(3, 7, 3)]
    [InlineData(7, 3, 3)]
    public void SelectedMemoryAndCitedEvidenceBoundResolution(int memoryMinutes, int evidenceMinutes, int expectedMinutes)
    {
        var job = SyntheticData.Job();
        var profile = SyntheticData.Profile();
        profile = profile with
        {
            Facts = [profile.Facts[0] with { ValidUntil = Now.AddMinutes(evidenceMinutes) }],
            Answers = [
                new() { SemanticKey = Preference.Key, Answer = "remote", ExpiresAt = Now.AddSeconds(1) },
                new() { SemanticKey = Preference.Key, Answer = "remote", Scope = AnswerScopeType.Company,
                    ScopeId = job.Employer, ExpiresAt = Now.AddMinutes(memoryMinutes), EvidenceIds = [profile.Facts[0].Id] }]
        };

        var answer = AnswerResolver.Resolve(Preference, profile, job, Now);

        Assert.Equal(AnswerStatus.Resolved, answer.Status);
        Assert.Equal(Now.AddMinutes(expectedMinutes), answer.ValidUntil);
    }

    [Fact]
    public void ProfessionalExperienceCarriesCitedFactDeadline()
    {
        var profile = SyntheticData.Profile();
        profile = profile with { Facts = [profile.Facts[0] with { ValidUntil = Now.AddMinutes(4) }] };
        var answer = AnswerResolver.Resolve(new() { Key = "experience.professional.csharp.years" },
            profile, SyntheticData.Job(), Now);
        Assert.Equal(AnswerStatus.Resolved, answer.Status);
        Assert.Equal(Now.AddMinutes(4), answer.ValidUntil);
    }

    [Fact]
    public void ResolvedDeadlineIsStoredAndHashBoundEvenWhenAnswerValueDoesNotChange()
    {
        var profile = SyntheticData.Profile() with
        {
            Answers = [new() { SemanticKey = Preference.Key, Answer = "remote", ExpiresAt = Now.AddMinutes(3) }]
        };
        var job = SyntheticData.Job();
        var draft = new ApplicationDraft { ProfileId = profile.Id, JobKey = job.Id };
        var first = ApplicationQuestions.Resolve(draft, [Preference], profile, job, Now).Draft;
        var later = ApplicationQuestions.Resolve(draft, [Preference], profile with
        { Answers = [profile.Answers[0] with { ExpiresAt = Now.AddMinutes(4) }] }, job, Now).Draft;

        Assert.Equal(Now.AddMinutes(3), first.AnswersValidUntil);
        Assert.Equal(first.Answers[Preference.Key], later.Answers[Preference.Key]);
        Assert.NotEqual(first.PayloadHash(), later.PayloadHash());
        var receipt = ApprovalPolicy.GrantFromUserInterface(first, ApprovalPurpose.Submit, "ui", Now);
        Assert.Equal("PackageChanged", Assert.Throws<PolicyException>(() =>
            ApprovalPolicy.Validate(later, receipt, ApprovalPurpose.Submit, Now)).Code);
    }

    [Theory]
    [InlineData(ApprovalPurpose.ShareData)]
    [InlineData(ApprovalPurpose.Submit)]
    public void ApprovalCannotOutliveAnswers(ApprovalPurpose purpose)
    {
        var draft = new ApplicationDraft { AnswersValidUntil = Now.AddMinutes(3) };
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, purpose, "ui", Now);
        Assert.Equal(draft.AnswersValidUntil, receipt.ExpiresAt);
        Assert.Equal("AnswersExpired", Assert.Throws<PolicyException>(() => ApprovalPolicy.Validate(draft,
            receipt with { ExpiresAt = Now.AddMinutes(20) }, purpose, Now.AddMinutes(3))).Code);
        Assert.Equal("AnswersExpired", Assert.Throws<PolicyException>(() =>
            ApprovalPolicy.GrantFromUserInterface(draft, purpose, "ui", Now.AddMinutes(3))).Code);
    }

    [Fact]
    public void LegacyMissingDeadlineRemainsNullableAndUsesTenMinuteApproval()
    {
        var draft = JsonSerializer.Deserialize<ApplicationDraft>("{}")!;
        Assert.Null(draft.AnswersValidUntil);
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.Submit, "ui", Now);
        Assert.Equal(Now.AddMinutes(10), receipt.ExpiresAt);
        ApprovalPolicy.Validate(draft, receipt, ApprovalPurpose.Submit, Now.AddMinutes(9));
    }
}
