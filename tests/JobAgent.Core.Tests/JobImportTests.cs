using JobAgent.Core.Jobs;

namespace JobAgent.Core.Tests;

public sealed class JobImportTests
{
    [Fact]
    public void CanonicalUrl_RemovesTrackingAndFragmentButKeepsIdentityQueries()
    {
        var canonical = JobUrlCanonicalizer.Normalize(
            "HTTPS://Careers.Example.Invalid/jobs/apply/?utm_source=mail&jobId=42&gh_jid=abc#details");

        Assert.Equal("https://careers.example.invalid/jobs/apply?gh_jid=abc&jobId=42", canonical);
    }

    [Fact]
    public void SameCompanyDifferentExternalIds_AreNotDuplicates()
    {
        var first = JobPostingImporter.ImportProvidedText("job-1", "Example Co", "Engineer",
            "Provided text", "https://careers.example.invalid/apply?jobId=1", TestProfiles.Now, synthetic: true);
        var second = JobPostingImporter.ImportProvidedText("job-2", "Example Co", "Engineer",
            "Provided text", "https://careers.example.invalid/apply?jobId=2", TestProfiles.Now, synthetic: true);

        Assert.NotEqual(first.DuplicateKey, second.DuplicateKey);
    }

    [Fact]
    public void ProvidedText_ProducesConservativeSuggestionsThatRequireReview()
    {
        const string text = """
            About the role
            Mandatory: 3 years of professional C# experience.
            Preferred: 5 years of professional C# experience.
            """;

        var proposal = JobPostingImporter.ProposeProvidedText("job-1", "Example Co", "C# Engineer",
            text, "https://careers.example.invalid/jobs/1?utm_campaign=test", TestProfiles.Now, synthetic: true);

        Assert.Empty(proposal.Posting.Requirements);
        Assert.True(proposal.RequiresUserReview);
        Assert.Collection(proposal.SuggestedRequirements,
            mandatory =>
            {
                Assert.Equal(RequirementImportance.Mandatory, mandatory.Importance);
                Assert.Equal(RequirementType.ProfessionalExperienceYears, mandatory.Type);
                Assert.Equal(3m, mandatory.MinimumYears);
                Assert.Equal("C#", mandatory.Skill);
                Assert.Equal("line 2", mandatory.SourceSpan);
                Assert.Equal(RequirementReviewStatus.Suggested, mandatory.ReviewStatus);
            },
            preferred =>
            {
                Assert.Equal(RequirementImportance.Preferred, preferred.Importance);
                Assert.Equal(5m, preferred.MinimumYears);
                Assert.Equal("line 3", preferred.SourceSpan);
            });

        var reviewed = JobPostingImporter.ConfirmRequirements(proposal,
            [proposal.SuggestedRequirements[0].Id], TestProfiles.Now);

        var confirmed = Assert.Single(reviewed.Requirements);
        Assert.Equal(RequirementReviewStatus.Confirmed, confirmed.ReviewStatus);
        Assert.Equal(TestProfiles.Now, reviewed.RequirementsReviewedAt);
    }

    [Fact]
    public void AmbiguousMarketingText_DoesNotFabricateRequirements()
    {
        var proposal = JobPostingImporter.ProposeProvidedText("job-1", "Example Co", "Engineer",
            "We seek a seasoned engineer with excellent modern technology experience.",
            "https://careers.example.invalid/jobs/1", TestProfiles.Now, synthetic: true);

        Assert.Empty(proposal.SuggestedRequirements);
        Assert.Empty(proposal.Posting.Requirements);
    }

    [Fact]
    public void InvalidTypedSuggestion_IsRejectedDuringReview()
    {
        var proposal = JobPostingImporter.ProposeProvidedText("job-1", "Example Co", "Engineer",
            "Mandatory: 3 years of professional C# experience.",
            "https://careers.example.invalid/jobs/1", TestProfiles.Now, synthetic: true);
        var invalid = proposal with
        {
            SuggestedRequirements =
            [
                proposal.SuggestedRequirements[0] with { MinimumYears = 0m }
            ]
        };

        Assert.Throws<InvalidDataException>(() => JobPostingImporter.ConfirmRequirements(invalid,
            [invalid.SuggestedRequirements[0].Id], TestProfiles.Now));
    }

    [Fact]
    public void ManuallyReviewedRequirement_CanConfirmTextTheParserDidNotSuggest()
    {
        const string requirementText = "You need at least 4 years working professionally with Go.";
        var proposal = JobPostingImporter.ProposeProvidedText("job-1", "Example Co", "Engineer",
            requirementText, "https://careers.example.invalid/jobs/1", TestProfiles.Now, synthetic: true);
        var reviewedRequirement = new JobRequirement
        {
            Id = "manual-go-experience",
            RequirementText = requirementText,
            Type = RequirementType.ProfessionalExperienceYears,
            Importance = RequirementImportance.Mandatory,
            Skill = "Go",
            MinimumYears = 4m,
            SourceSpan = "line 1",
            RuleRationale = "User reviewed the supplied job text and entered the typed requirement.",
            ReviewStatus = RequirementReviewStatus.Suggested
        };

        var reviewed = JobPostingImporter.ConfirmReviewedRequirements(proposal,
            [reviewedRequirement], TestProfiles.Now);

        var confirmed = Assert.Single(reviewed.Requirements);
        Assert.Equal(RequirementReviewStatus.Confirmed, confirmed.ReviewStatus);
        Assert.Equal(TestProfiles.Now, reviewed.RequirementsReviewedAt);

        var absentFromSource = reviewedRequirement with { RequirementText = "5 years of Rust required." };
        Assert.Throws<InvalidDataException>(() => JobPostingImporter.ConfirmReviewedRequirements(proposal,
            [absentFromSource], TestProfiles.Now));
    }
}
