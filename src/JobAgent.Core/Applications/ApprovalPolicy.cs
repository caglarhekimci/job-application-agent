namespace JobAgent.Core.Applications;

public static class ApprovalPolicy
{
    public static ApprovalReceipt GrantFromUserInterface(ApplicationDraft draft,
        ApprovalPurpose purpose, string userSessionId, DateTimeOffset now) =>
        new(Guid.NewGuid(), draft.Id, draft.PayloadHash(), draft.RecipientOrigin,
            draft.ProfileVersion, draft.ResumeHash, userSessionId, purpose, now, now.AddMinutes(10));

    public static void Validate(ApplicationDraft draft, ApprovalReceipt? receipt,
        ApprovalPurpose purpose, DateTimeOffset now)
    {
        if (receipt is null || string.IsNullOrWhiteSpace(receipt.UserSessionId))
            throw new PolicyException("ConsentRequired");
        if (receipt.UsedAt is not null) throw new PolicyException("ApprovalAlreadyUsed");
        if (receipt.ExpiresAt <= now || receipt.ApprovedAt > now)
            throw new PolicyException("ApprovalExpired");
        if (receipt.Purpose != purpose) throw new PolicyException("WrongApprovalPurpose");
        if (receipt.ApplicationId != draft.Id || receipt.PayloadHash != draft.PayloadHash()
            || receipt.RecipientOrigin != draft.RecipientOrigin || receipt.ResumeHash != draft.ResumeHash
            || receipt.ProfileVersion != draft.ProfileVersion)
            throw new PolicyException("PackageChanged");
        if (draft.Status is ApplicationStatus.Cancelled or ApplicationStatus.Submitting
            or ApplicationStatus.SubmittedVerified or ApplicationStatus.SubmittedUnverified)
            throw new PolicyException("ApplicationNotActionable");
    }

    public static ApprovalReceipt Consume(ApplicationDraft draft, ApprovalReceipt receipt,
        ApprovalPurpose purpose, DateTimeOffset now)
    {
        Validate(draft, receipt, purpose, now);
        return receipt with { UsedAt = now };
    }
}
