using JobAgent.Core.Jobs;

namespace JobAgent.Core.Tests;

public sealed class JobEvaluationTests
{
    [Fact]
    public void MandatoryYears_AndPreferredYears_AreDifferent()
    {
        var mandatory = JobAgent.Core.SyntheticData.Job() with
        {
            Requirements = [new() { RequirementText = "5 years C# required", Type = RequirementType.ProfessionalExperienceYears, Importance = RequirementImportance.Mandatory, Skill = "C#", MinimumYears = 5 }]
        };
        var preferred = mandatory with
        {
            Requirements = [mandatory.Requirements[0] with { RequirementText = "5 years C# preferred", Importance = RequirementImportance.Preferred }]
        };

        Assert.Equal(JobEvaluationStatus.HardRequirementMismatch, JobEvaluator.Evaluate(mandatory, TestProfiles.Synthetic(), TestProfiles.Now).Status);
        Assert.Equal(JobEvaluationStatus.ReviewNeeded, JobEvaluator.Evaluate(preferred, TestProfiles.Synthetic(), TestProfiles.Now).Status);
    }

    [Fact]
    public void UnknownWorkAuthorization_IsNotMatch()
    {
        var job = JobAgent.Core.SyntheticData.Job() with
        {
            Requirements = [new() { RequirementText = "Work authorization required", Type = RequirementType.WorkAuthorization, Importance = RequirementImportance.Mandatory }]
        };

        var result = JobEvaluator.Evaluate(job, TestProfiles.Synthetic(), TestProfiles.Now);

        Assert.Equal(JobEvaluationStatus.InsufficientInformation, result.Status);
        Assert.Equal(RequirementAssessment.Unknown, result.Requirements.Single().Assessment);
    }

    [Fact]
    public void SameTitleDifferentEmployer_IsNotDuplicate()
    {
        var first = JobAgent.Core.SyntheticData.Job() with { Id = "42", Employer = "One", Title = "Engineer" };
        var second = first with { Employer = "Two" };

        Assert.NotEqual(first.DuplicateKey, second.DuplicateKey);
    }

    [Fact]
    public void OverlappingProfessionalPeriods_DoNotDoubleCountCalendarTime()
    {
        var profile = TestProfiles.Synthetic();
        profile = profile with
        {
            Experience = [profile.Experience[0], profile.Experience[0] with { Role = "Concurrent role" }]
        };
        var job = JobAgent.Core.SyntheticData.Job() with
        {
            Requirements = [new() { RequirementText = "5 years C# required", Type = RequirementType.ProfessionalExperienceYears, Importance = RequirementImportance.Mandatory, Skill = "C#", MinimumYears = 5 }]
        };

        var result = JobEvaluator.Evaluate(job, profile, TestProfiles.Now);

        Assert.Equal(JobEvaluationStatus.HardRequirementMismatch, result.Status);
    }

    [Fact]
    public void ClosedJob_IsNeverEligible()
    {
        var job = JobAgent.Core.SyntheticData.Job() with
        {
            Availability = JobAvailability.Closed,
            ClosedAt = TestProfiles.Now.AddDays(-1)
        };

        var result = JobEvaluator.Evaluate(job, TestProfiles.Synthetic(), TestProfiles.Now);

        Assert.Equal(JobEvaluationStatus.Closed, result.Status);
        Assert.Contains("closed", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportedJobWithoutReviewedRequirements_IsInsufficientInformation()
    {
        var job = JobPostingImporter.ImportProvidedText("job-1", "Synthetic Employer", "Engineer",
            "Ambiguous supplied text without explicit requirements.",
            "https://careers.example.invalid/jobs/1", TestProfiles.Now, synthetic: true);

        var result = JobEvaluator.Evaluate(job, TestProfiles.Synthetic(), TestProfiles.Now);

        Assert.Equal(JobEvaluationStatus.InsufficientInformation, result.Status);
        Assert.Contains("reviewed", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
