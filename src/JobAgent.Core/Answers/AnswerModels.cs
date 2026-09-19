namespace JobAgent.Core.Answers;

public enum AnswerStatus { Resolved, NeedsInput, RequiresReview, ManualOnly, Blocked }

public sealed record AnswerScopeContext(Guid ApplicationId, string RoleGroupId);

public sealed record FormQuestion
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Language { get; init; } = "en";
    public int? MaxLength { get; init; }
    public bool Sensitive { get; init; }
    public bool RequiresCandidateAttestation { get; init; }
}

public sealed record AnswerResolution
{
    public AnswerStatus Status { get; init; }
    public string? Value { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<string> EvidenceIds { get; init; } = [];
    public DateTimeOffset? ValidUntil { get; init; }
}
