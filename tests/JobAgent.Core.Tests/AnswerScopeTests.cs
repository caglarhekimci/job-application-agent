using JobAgent.Core.Answers;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class AnswerScopeTests
{
    [Fact]
    public void CompanyScopedAnswer_IsNotGlobal()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers = [new() { SemanticKey = "relocation", Answer = "Yes", Scope = AnswerScopeType.Company, ScopeId = "Other Co", Language = "en", UpdatedAt = TestProfiles.Now }]
        };
        var question = new FormQuestion { Key = "relocation", Label = "Will you relocate?", Language = "en" };

        var result = AnswerResolver.Resolve(question, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.NeedsInput, result.Status);
    }

    [Fact]
    public void ExpiredAnswer_RequiresReview()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers = [new() { SemanticKey = "availability", Answer = "2 weeks", Scope = AnswerScopeType.Default, Language = "en", UpdatedAt = TestProfiles.Now.AddMonths(-2), ExpiresAt = TestProfiles.Now.AddSeconds(-1) }]
        };

        var result = AnswerResolver.Resolve(new() { Key = "availability", Label = "Notice period", Language = "en" }, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.RequiresReview, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void AnswerExpiringNow_RequiresReview()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers = [new() { SemanticKey = "availability", Answer = "2 weeks", Scope = AnswerScopeType.Default, Language = "en", UpdatedAt = TestProfiles.Now.AddMonths(-2), ExpiresAt = TestProfiles.Now }]
        };

        var result = AnswerResolver.Resolve(new() { Key = "availability", Label = "Notice period", Language = "en" }, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.RequiresReview, result.Status);
    }

    [Fact]
    public void AnswerWithMissingEvidence_RequiresReview()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers = [new() { SemanticKey = "availability", Answer = "2 weeks", Scope = AnswerScopeType.Default, Language = "en", UpdatedAt = TestProfiles.Now, EvidenceIds = ["missing-fact"] }]
        };

        var result = AnswerResolver.Resolve(new() { Key = "availability", Label = "Notice period", Language = "en" }, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.RequiresReview, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TurkishQuestion_UsesTurkishAnswer()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers =
            [
                new() { SemanticKey = "availability", Answer = "Two weeks", Scope = AnswerScopeType.Default, Language = "en", UpdatedAt = TestProfiles.Now },
                new() { SemanticKey = "availability", Answer = "İki hafta", Scope = AnswerScopeType.Default, Language = "tr", UpdatedAt = TestProfiles.Now }
            ]
        };

        var result = AnswerResolver.Resolve(new() { Key = "availability", Label = "İhbar süreniz?", Language = "tr" }, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal("İki hafta", result.Value);
    }

    [Fact]
    public void UnknownKey_Abstains()
    {
        var result = AnswerResolver.Resolve(new() { Key = "unknown.claim", Label = "Invent something" }, TestProfiles.Synthetic(), JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.NeedsInput, result.Status);
        Assert.Null(result.Value);
    }
}
