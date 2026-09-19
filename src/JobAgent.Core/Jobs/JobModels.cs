using JobAgent.Core.Permissions;

namespace JobAgent.Core.Jobs;

public enum RequirementType { ProfessionalExperienceYears, WorkAuthorization, Skill, Language, Location, Contract }
public enum RequirementImportance { Mandatory, Preferred }
public enum RequirementAssessment { Match, Mismatch, Unknown, NotApplicable }
public enum RequirementReviewStatus { Confirmed, Suggested }
public enum JobAvailability { Open, Closed, Unknown }
public enum JobEvaluationStatus { Eligible, ReviewNeeded, HardRequirementMismatch, InsufficientInformation, Closed }

public sealed record JobRequirement
{
    public string Id { get; init; } = string.Empty;
    public string RequirementText { get; init; } = string.Empty;
    public RequirementType Type { get; init; }
    public RequirementImportance Importance { get; init; }
    public string? Skill { get; init; }
    public decimal? MinimumYears { get; init; }
    public string? ExpectedValue { get; init; }
    public string SourceSpan { get; init; } = string.Empty;
    public string RuleRationale { get; init; } = string.Empty;
    public RequirementReviewStatus ReviewStatus { get; init; } = RequirementReviewStatus.Confirmed;
}

public sealed record JobPosting
{
    public string Id { get; init; } = string.Empty;
    public string Employer { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
    public string CanonicalUrl { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string TextHash { get; init; } = string.Empty;
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastCheckedAt { get; init; }
    public DateTimeOffset? RequirementsReviewedAt { get; init; }
    public JobAvailability Availability { get; init; } = JobAvailability.Open;
    public DateTimeOffset? ClosedAt { get; init; }
    public bool Synthetic { get; init; }
    public List<JobRequirement> Requirements { get; init; } = [];
    public SourcePermission SourcePermission { get; init; } = SourcePermission.ForUserProvidedText();

    public string DuplicateKey => JobIdentity.Create(this);
}

public sealed record RequirementResult
{
    public string RequirementText { get; init; } = string.Empty;
    public RequirementType RequirementType { get; init; }
    public List<string> CandidateEvidenceIds { get; init; } = [];
    public RequirementAssessment Assessment { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record JobEvaluation
{
    public JobEvaluationStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<RequirementResult> Requirements { get; init; } = [];
}

public sealed record JobImportProposal
{
    public JobPosting Posting { get; init; } = new();
    public List<JobRequirement> SuggestedRequirements { get; init; } = [];
    public bool RequiresUserReview => true;
}

public static class JobIdentity
{
    public static string Create(JobPosting job)
    {
        var source = job.SourcePermission.Source.ToString().ToUpperInvariant();
        var employer = job.Employer.Trim().ToUpperInvariant();
        var externalId = string.IsNullOrWhiteSpace(job.ExternalId) ? job.Id : job.ExternalId;
        if (!string.IsNullOrWhiteSpace(externalId))
            return $"{source}|{employer}|ID:{externalId.Trim().ToUpperInvariant()}";
        if (!string.IsNullOrWhiteSpace(job.CanonicalUrl))
            return $"{source}|{employer}|URL:{job.CanonicalUrl}";
        return $"{source}|{employer}|TEXT:{job.TextHash}";
    }
}

public static class JobRequirementPolicy
{
    public static void Validate(JobRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.Id) || string.IsNullOrWhiteSpace(requirement.RequirementText))
            throw new InvalidDataException("Requirement identity and text are required.");
        if (requirement.Type == RequirementType.ProfessionalExperienceYears &&
            (requirement.MinimumYears is null or <= 0 || string.IsNullOrWhiteSpace(requirement.Skill)))
            throw new InvalidDataException("Professional experience requires a positive duration and skill.");
        if (requirement.ReviewStatus == RequirementReviewStatus.Suggested &&
            string.IsNullOrWhiteSpace(requirement.SourceSpan))
            throw new InvalidDataException("Suggested requirements require a source span.");
    }
}
