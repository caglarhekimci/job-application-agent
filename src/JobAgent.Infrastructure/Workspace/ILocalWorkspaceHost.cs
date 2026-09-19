using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Jobs;

namespace JobAgent.Infrastructure.Workspace;

// A projection over the same protected workspace used by the local UI. This contract
// deliberately has no confirmation, approval, document-path, or browser-navigation input.
public interface ILocalWorkspaceHost
{
    Task<HostWorkspaceRefs> GetHostWorkspaceRefsAsync();
    Task<HostProfileSummary> GetHostProfileSummaryAsync(Guid profileRef);
    Task<HostPendingProposal> ProposeProfilePatchAsync(HostProfilePatchRequest request);
    Task<HostJobImportResult> ImportJobProposalAsync(HostJobImportRequest request);
    Task<HostJobEvaluation> EvaluateJobAsync(Guid jobRef, Guid profileRef);
    Task<HostApplicationState> CreateApplicationAsync(HostCreateApplicationRequest request);
    Task<HostApplicationQuestions> GetApplicationQuestionsAsync(Guid applicationRef);
    Task<HostAnswerProposalResult> ProposeApplicationAnswersAsync(HostAnswerProposalRequest request);
    Task<HostApplicationState> PrepareApplicationReviewAsync(Guid applicationRef);
    Task<HostApplicationState> ExecuteApprovedApplicationAsync(Guid applicationRef);
    Task<HostApplicationState> GetApplicationStatusAsync(Guid applicationRef);
    Task<HostApplicationState> CancelApplicationAsync(Guid applicationRef);
}

public sealed record HostWorkspaceRefs(long Revision, Guid? ProfileRef, int? ProfileVersion,
    Guid? ResumeRef, Guid? JobRef);
public sealed record HostSkillEvidence(string EvidenceRef, string Skill);
public sealed record HostProfileSummary(Guid ProfileRef, int Version,
    IReadOnlyList<string> VerifiedSkills, IReadOnlyList<HostSkillEvidence> Evidence);
public enum HostProfileField { FullName, Email, Locale, ProfessionalSkill }
public sealed record HostProfileChange(HostProfileField Field, string Value, IReadOnlyList<string> EvidenceRefs);
public sealed record HostProfilePatchRequest(Guid ProfileRef, int BaseVersion, IReadOnlyList<HostProfileChange> Changes);
public sealed record HostPendingProposal(Guid ProposalRef, long Revision, int BaseVersion,
    string Status, bool RequiresUserReview);
public sealed record HostJobImportRequest(string Text, string? SourceUrl = null,
    string? Employer = null, string? Title = null);
public sealed record HostJobImportResult(Guid JobRef, long Revision, bool RequiresUserReview,
    IReadOnlyList<JobRequirement> SuggestedRequirements);
public sealed record HostJobEvaluation(Guid JobRef, Guid ProfileRef, int ProfileVersion,
    long Revision, JobEvaluation Evaluation);
public sealed record HostCreateApplicationRequest(Guid JobRef, Guid ProfileRef, Guid ResumeRef);
public sealed record HostSubmissionEvidence(string ReceiptId, DateTimeOffset VerifiedAt);
public sealed record HostApplicationState(Guid ApplicationRef, long Revision, ApplicationStatus Status,
    bool ReviewRequested, bool SubmissionApproved, string? ReviewRef, int UnresolvedQuestions,
    HostSubmissionEvidence? Evidence = null);
public sealed record HostQuestionSummary(string Key, string Label, string Language, int? MaxLength,
    AnswerStatus Status, bool ManualOnly, IReadOnlyList<string> EvidenceRefs);
public sealed record HostApplicationQuestions(Guid ApplicationRef, long Revision,
    IReadOnlyList<HostQuestionSummary> Questions);
public sealed record HostAnswerProposal(int SchemaVersion, string SemanticKey, string ProposedValue,
    string Language, IReadOnlyList<string> EvidenceIds, string Rationale);
public sealed record HostAnswerProposalRequest(Guid ApplicationRef, long BaseRevision,
    IReadOnlyList<HostAnswerProposal> Answers, IReadOnlyList<string> EvidenceRefs);
public sealed record HostAnswerProposalItem(string SemanticKey, AnswerProposalDisposition Disposition, string Reason);
public sealed record HostAnswerProposalResult(Guid ApplicationRef, long Revision,
    IReadOnlyList<HostAnswerProposalItem> Results, bool RequiresUserReview);
