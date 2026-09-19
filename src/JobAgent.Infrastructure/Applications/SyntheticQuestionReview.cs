using JobAgent.Core.Answers;
using JobAgent.Core.Applications;

namespace JobAgent.Infrastructure.Applications;

public sealed record SyntheticQuestionResolution(
    string Key,
    string Label,
    string Language,
    int? MaxLength,
    bool Sensitive,
    bool RequiresCandidateAttestation,
    AnswerStatus Status,
    string? Value,
    string Reason);

public sealed record SyntheticQuestionReview(
    Guid ApplicationRef,
    string PayloadHash,
    ApplicationStatus Status,
    IReadOnlyList<SyntheticQuestionResolution> Questions);
