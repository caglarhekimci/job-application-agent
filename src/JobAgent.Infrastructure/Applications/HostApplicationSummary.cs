using JobAgent.Core.Applications;

namespace JobAgent.Infrastructure.Applications;

public sealed record HostApplicationSummary(
    Guid ApplicationRef,
    ApplicationStatus Status,
    bool ReviewRequested,
    bool SubmissionApproved,
    string? ReceiptId);
