namespace JobAgent.Core.Profiles;

public enum SalaryPeriod { Month, Year }
public enum TaxBasis { Net, Gross }
public enum VerificationStatus { Proposed, Verified, Rejected }
public enum ExperienceKind { Professional, Internship, PartTime, PersonalProject }
// Preserve persisted numeric values; specificity is ordered explicitly by the resolver.
public enum AnswerScopeType { Default, Company, Application, RoleGroup }
public enum ProfileField { FullName, Email, Salary }
public enum PatchReviewStatus { Pending, Applied, Rejected, Conflict }

public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public SalaryPeriod Period { get; init; }
    public TaxBasis TaxBasis { get; init; }
}

public sealed record SalaryPreference
{
    public Money Target { get; init; } = new();
    public Money PrivateMinimum { get; init; } = new();
    public bool Negotiable { get; init; }
    public bool DisclosePrivateMinimum { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
}

public sealed record EvidenceFact
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string SourceDocumentId { get; init; } = string.Empty;
    public string SourceSpan { get; init; } = string.Empty;
    public decimal? Confidence { get; init; }
    public VerificationStatus VerificationStatus { get; init; } = VerificationStatus.Proposed;
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidUntil { get; init; }
}

public sealed record ExperiencePeriod
{
    public DateOnly Start { get; init; }
    public DateOnly? End { get; init; }
    public string Role { get; init; } = string.Empty;
    public ExperienceKind Kind { get; init; }
    public decimal EmploymentFraction { get; init; } = 1m;
    public List<string> Skills { get; init; } = [];
    public List<string> EvidenceIds { get; init; } = [];
}

public sealed record AnswerMemory
{
    public string SemanticKey { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public AnswerScopeType Scope { get; init; } = AnswerScopeType.Default;
    public string? ScopeId { get; init; }
    public string Language { get; init; } = "en";
    public List<string> EvidenceIds { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record ConsentPolicy
{
    public string RecipientScope { get; init; } = string.Empty;
    public List<string> DataCategories { get; init; } = [];
    public string Purpose { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
}

public sealed record LocalConfirmation
{
    public ProfileField Field { get; init; }
    public DateTimeOffset ConfirmedAt { get; init; }
}

public sealed record CandidateProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Version { get; init; } = 1;
    public bool Synthetic { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Locale { get; init; } = "en";
    public DateTimeOffset? VerifiedAt { get; init; }
    public List<EvidenceFact> Facts { get; init; } = [];
    public List<ExperiencePeriod> Experience { get; init; } = [];
    public SalaryPreference Salary { get; init; } = new();
    public List<AnswerMemory> Answers { get; init; } = [];
    public List<ConsentPolicy> Consents { get; init; } = [];
    public List<LocalConfirmation> LocalConfirmations { get; init; } = [];
}

public sealed record ProfilePatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProfileId { get; init; }
    public int BaseVersion { get; init; }
    public CandidateProfile ProposedProfile { get; init; } = new();
    public PatchReviewStatus ReviewStatus { get; init; } = PatchReviewStatus.Pending;
    public DateTimeOffset ProposedAt { get; init; }
}

public sealed record ProfilePatchResult
{
    public bool Applied { get; init; }
    public CandidateProfile? Profile { get; init; }
    public string? Error { get; init; }
}
