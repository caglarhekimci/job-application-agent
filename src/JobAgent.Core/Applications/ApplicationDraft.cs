using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobAgent.Core.Answers;

namespace JobAgent.Core.Applications;

public enum ApplicationStatus
{
    Drafting, NeedsInput, ReadyForDataSharing, Filling, AwaitingSubmissionApproval,
    Submitting, SubmittedVerified, SubmittedUnverified, FailedBeforeSubmission,
    Cancelled, BlockedPermission
}

public sealed record ApplicationDraft
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProfileId { get; init; }
    public int ProfileVersion { get; init; }
    public string JobKey { get; init; } = "";
    public string JobTitle { get; init; } = "";
    public string Employer { get; init; } = "";
    public string RecipientOrigin { get; init; } = "";
    public string TargetPath { get; init; } = "/jobs/synthetic-dotnet";
    public string ResumeRef { get; init; } = "";
    public string ResumeHash { get; init; } = "";
    public SortedDictionary<string, string> Answers { get; init; } = new(StringComparer.Ordinal);
    public List<FormQuestion> Questions { get; init; } = [];
    public DateTimeOffset? AnswersValidUntil { get; init; }
    public ApplicationStatus Status { get; init; } = ApplicationStatus.ReadyForDataSharing;
    public bool Synthetic { get; init; }

    public string PayloadHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            Id,
            ProfileId,
            ProfileVersion,
            JobKey,
            JobTitle,
            Employer,
            RecipientOrigin,
            TargetPath,
            ResumeRef,
            ResumeHash,
            Answers = Answers.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray(),
            Questions,
            AnswersValidUntil,
            Synthetic
        }))));
}

public enum ApprovalPurpose { ShareData, Submit }

public sealed record ApprovalReceipt(Guid Id, Guid ApplicationId, string PayloadHash,
    string RecipientOrigin, int ProfileVersion, string ResumeHash, string UserSessionId,
    ApprovalPurpose Purpose, DateTimeOffset ApprovedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset? UsedAt = null);

public sealed record SubmissionEvidence(string ReceiptId, string ApplicationKey,
    string ResumeHash, DateTimeOffset VerifiedAt, string PayloadHash = "");

public sealed class PolicyException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
