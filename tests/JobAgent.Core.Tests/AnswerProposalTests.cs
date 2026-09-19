using JobAgent.Core.Answers;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class AnswerProposalTests
{
    [Fact]
    public void GroundedModelOutput_RemainsProposedForUserReview()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "semanticKey": "skill.primary",
              "proposedValue": "C#",
              "language": "en",
              "evidenceIds": ["fact-csharp-professional"],
              "rationale": "The supplied evidence explicitly names C#."
            }
            """;

        var result = ModelAnswerProposalValidator.ValidateJson(json,
            new() { Key = "skill.primary", Label = "Primary skill", Language = "en", MaxLength = 20 },
            TestProfiles.Synthetic(), ["fact-csharp-professional"], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.RequiresReview, result.Disposition);
        Assert.Equal(AnswerProposalReviewStatus.Proposed, result.Proposal!.ReviewStatus);
        Assert.Equal("C#", result.Proposal.ProposedValue);
        Assert.Equal(["fact-csharp-professional"], result.Proposal.EvidenceIds);
    }

    [Fact]
    public void UnverifiedEvidence_Abstains()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Facts = [TestProfiles.Synthetic().Facts[0] with { VerificationStatus = VerificationStatus.Proposed }]
        };

        var result = ModelAnswerProposalValidator.ValidateJson(ValidJson(),
            new() { Key = "skill.primary", Label = "Primary skill", Language = "en" },
            profile, ["fact-csharp-professional"], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.Abstained, result.Disposition);
        Assert.Null(result.Proposal);
    }

    [Fact]
    public void EvidenceAtExactValidityEnd_Abstains()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Facts = [TestProfiles.Synthetic().Facts[0] with { ValidUntil = TestProfiles.Now }]
        };

        var result = ModelAnswerProposalValidator.ValidateJson(ValidJson(),
            new() { Key = "skill.primary", Label = "Primary skill", Language = "en" },
            profile, ["fact-csharp-professional"], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.Abstained, result.Disposition);
        Assert.Null(result.Proposal);
        var resolution = AnswerResolver.Resolve(new() { Key = "availability", Label = "Availability", Language = "en" },
            profile with
            {
                Answers = [new() { SemanticKey = "availability", Language = "en", Answer = "Two weeks",
                    EvidenceIds = ["fact-csharp-professional"] }]
            }, new(), TestProfiles.Now);
        Assert.Equal(AnswerStatus.RequiresReview, resolution.Status);
    }

    [Fact]
    public void EvidenceOutsideSuppliedModelContext_Abstains()
    {
        var result = ModelAnswerProposalValidator.ValidateJson(ValidJson(),
            new() { Key = "skill.primary", Label = "Primary skill", Language = "en" },
            TestProfiles.Synthetic(), [], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.Abstained, result.Disposition);
        Assert.Null(result.Proposal);
    }

    [Fact]
    public void SchemaDriftOrSensitiveQuestion_Abstains()
    {
        var drifted = ValidJson().Replace("\n}", ",\n  \"confidence\": 0.99\n}", StringComparison.Ordinal);
        var schemaResult = ModelAnswerProposalValidator.ValidateJson(drifted,
            new() { Key = "skill.primary", Label = "Primary skill", Language = "en" },
            TestProfiles.Synthetic(), ["fact-csharp-professional"], TestProfiles.Now);
        var sensitiveResult = ModelAnswerProposalValidator.ValidateJson(ValidJson(),
            new() { Key = "skill.primary", Label = "Health status", Language = "en", Sensitive = true },
            TestProfiles.Synthetic(), ["fact-csharp-professional"], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.Abstained, schemaResult.Disposition);
        Assert.Equal(AnswerProposalDisposition.Abstained, sensitiveResult.Disposition);
    }

    [Fact]
    public void DuplicateSchemaKey_AbstainsExplicitly()
    {
        var duplicate = ValidJson().Replace(
            "\"semanticKey\": \"skill.primary\",",
            "\"semanticKey\": \"skill.primary\",\n  \"semanticKey\": \"skill.primary\",",
            StringComparison.Ordinal);

        var result = ModelAnswerProposalValidator.ValidateJson(duplicate,
            new() { Key = "skill.primary", Label = "Primary skill", Language = "en" },
            TestProfiles.Synthetic(), ["fact-csharp-professional"], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.Abstained, result.Disposition);
        Assert.Contains("duplicate", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OversizedJsonAndValue_AbstainAtFixedBounds()
    {
        var oversizedJson = new string(' ', 32_769) + ValidJson();
        var oversizedValue = ValidJson().Replace("\"C#\"", $"\"{new string('x', 4_001)}\"",
            StringComparison.Ordinal);
        var question = new FormQuestion
        {
            Key = "skill.primary",
            Label = "Primary skill",
            Language = "en",
            MaxLength = 10_000
        };

        var jsonResult = ModelAnswerProposalValidator.ValidateJson(oversizedJson, question,
            TestProfiles.Synthetic(), ["fact-csharp-professional"], TestProfiles.Now);
        var valueResult = ModelAnswerProposalValidator.ValidateJson(oversizedValue, question,
            TestProfiles.Synthetic(), ["fact-csharp-professional"], TestProfiles.Now);

        Assert.Equal(AnswerProposalDisposition.Abstained, jsonResult.Disposition);
        Assert.Contains("size", jsonResult.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AnswerProposalDisposition.Abstained, valueResult.Disposition);
    }

    private static string ValidJson() => """
        {
          "schemaVersion": 1,
          "semanticKey": "skill.primary",
          "proposedValue": "C#",
          "language": "en",
          "evidenceIds": ["fact-csharp-professional"],
          "rationale": "The supplied evidence explicitly names C#."
        }
        """;
}
