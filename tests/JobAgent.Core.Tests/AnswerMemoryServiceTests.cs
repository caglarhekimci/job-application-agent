using JobAgent.Core.Answers;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class AnswerMemoryServiceTests
{
    [Fact]
    public void UpsertReviewed_ReplacesOnlyExactScopedAnswer()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers =
            [
                new() { SemanticKey = "availability", Answer = "Four weeks", Language = "en",
                    Scope = AnswerScopeType.Default, UpdatedAt = TestProfiles.Now.AddDays(-2) },
                new() { SemanticKey = "availability", Answer = "Three weeks", Language = "en",
                    Scope = AnswerScopeType.Company, ScopeId = "Synthetic Employer", UpdatedAt = TestProfiles.Now.AddDays(-1) }
            ]
        };

        var updated = AnswerMemoryService.UpsertReviewed(profile, new()
        {
            SemanticKey = "availability",
            Answer = "Two weeks",
            Language = "en",
            Scope = AnswerScopeType.Company,
            ScopeId = "Synthetic Employer",
            EvidenceIds = ["fact-csharp-professional"]
        }, TestProfiles.Now);

        Assert.Equal(profile.Version + 1, updated.Version);
        Assert.Equal(2, updated.Answers.Count);
        Assert.Contains(updated.Answers, item => item.Scope == AnswerScopeType.Default && item.Answer == "Four weeks");
        var company = Assert.Single(updated.Answers, item => item.Scope == AnswerScopeType.Company);
        Assert.Equal("Two weeks", company.Answer);
        Assert.Equal(TestProfiles.Now, company.UpdatedAt);
    }

    [Fact]
    public void Revoke_RemovesOnlyExactScopedAnswer()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Answers =
            [
                new() { SemanticKey = "availability", Answer = "Four weeks", Language = "en",
                    Scope = AnswerScopeType.Default, UpdatedAt = TestProfiles.Now.AddDays(-2) },
                new() { SemanticKey = "availability", Answer = "Two weeks", Language = "en",
                    Scope = AnswerScopeType.Company, ScopeId = "Synthetic Employer", UpdatedAt = TestProfiles.Now.AddDays(-1) }
            ]
        };

        var updated = AnswerMemoryService.Revoke(profile, new()
        {
            SemanticKey = "availability",
            Language = "en",
            Scope = AnswerScopeType.Company,
            ScopeId = "Synthetic Employer"
        });

        Assert.Equal(profile.Version + 1, updated.Version);
        var remaining = Assert.Single(updated.Answers);
        Assert.Equal(AnswerScopeType.Default, remaining.Scope);
    }

    [Fact]
    public void UpsertReviewed_RejectsMissingEvidenceAndInvalidScope()
    {
        var missingEvidence = new ReviewedAnswerMemoryUpdate
        {
            SemanticKey = "availability",
            Answer = "Two weeks",
            Language = "en",
            Scope = AnswerScopeType.Default,
            EvidenceIds = ["missing"]
        };
        var invalidScope = missingEvidence with
        {
            EvidenceIds = [],
            Scope = AnswerScopeType.Company,
            ScopeId = null
        };

        Assert.Throws<InvalidDataException>(() => AnswerMemoryService.UpsertReviewed(
            TestProfiles.Synthetic(), missingEvidence, TestProfiles.Now));
        Assert.Throws<InvalidDataException>(() => AnswerMemoryService.UpsertReviewed(
            TestProfiles.Synthetic(), invalidScope, TestProfiles.Now));
    }

    [Fact]
    public void UpsertReviewed_RejectsNullOrUnboundedEvidenceList()
    {
        var facts = Enumerable.Range(1, 21).Select(index => new EvidenceFact
        {
            Id = $"fact-{index}",
            Kind = "test",
            Value = index.ToString(),
            SourceDocumentId = "test-document",
            SourceSpan = $"line {index}",
            VerificationStatus = VerificationStatus.Verified,
            ValidFrom = TestProfiles.Now.AddDays(-1)
        }).ToList();
        var profile = TestProfiles.Synthetic() with { Facts = facts };
        var template = new ReviewedAnswerMemoryUpdate
        {
            SemanticKey = "availability",
            Answer = "Two weeks",
            Language = "en",
            Scope = AnswerScopeType.Default
        };

        Assert.Throws<InvalidDataException>(() => AnswerMemoryService.UpsertReviewed(profile,
            template with { EvidenceIds = null! }, TestProfiles.Now));
        Assert.Throws<InvalidDataException>(() => AnswerMemoryService.UpsertReviewed(profile,
            template with { EvidenceIds = facts.Select(fact => fact.Id).ToList() }, TestProfiles.Now));
    }
}
