namespace JobAgent.Core.Permissions;

public enum SourceKind { UserProvidedPosting, AuthorizedFeed, AuthorizedCareerSite, LinkedInRestricted }
public enum SourceAction { AnalyzeProvidedText, Fetch, Search, ReadPage, Autofill, Submit }
public enum PermissionStatus { Allowed, PermissionRequired, Denied }

public sealed record SourcePermission
{
    public SourceKind Source { get; init; } = SourceKind.UserProvidedPosting;
    public PermissionStatus Status { get; init; } = PermissionStatus.PermissionRequired;
    public HashSet<SourceAction> AllowedActions { get; init; } = [];
    public DateTimeOffset VerifiedAt { get; init; }
    public DateTimeOffset? ReviewAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string EvidenceId { get; init; } = string.Empty;
    public string RecipientOrigin { get; init; } = string.Empty;

    public bool Allows(SourceAction action, DateTimeOffset now) =>
        Allows(action, null, now);

    public bool Allows(SourceAction action, string? recipientOrigin, DateTimeOffset now)
    {
        if (Source == SourceKind.LinkedInRestricted || Status != PermissionStatus.Allowed ||
            (ReviewAt is not null && ReviewAt <= now) ||
            (ExpiresAt is not null && ExpiresAt <= now) || !AllowedActions.Contains(action))
            return false;
        if (action == SourceAction.AnalyzeProvidedText)
            return true;
        return !string.IsNullOrWhiteSpace(recipientOrigin) &&
            string.Equals(NormalizeOrigin(recipientOrigin), RecipientOrigin, StringComparison.OrdinalIgnoreCase);
    }

    public static SourcePermission ForUserProvidedText(DateTimeOffset? verifiedAt = null,
        string? recipientOrigin = null) => new()
        {
            Source = SourceKind.UserProvidedPosting,
            Status = PermissionStatus.Allowed,
            AllowedActions = new HashSet<SourceAction> { SourceAction.AnalyzeProvidedText },
            VerifiedAt = verifiedAt ?? DateTimeOffset.UtcNow,
            Scope = "provided-text-only",
            EvidenceId = "user-provided-text",
            RecipientOrigin = NormalizeOrigin(recipientOrigin)
        };

    public static SourcePermission ForAuthorizedSource(SourceKind source,
        IEnumerable<SourceAction> allowedActions, string evidenceId, string scope,
        string recipientOrigin, DateTimeOffset verifiedAt, DateTimeOffset expiresAt,
        DateTimeOffset? reviewAt = null)
    {
        if (source is not (SourceKind.AuthorizedFeed or SourceKind.AuthorizedCareerSite))
            throw new ArgumentException("Only an authorized feed or career site can use this factory.", nameof(source));
        return new()
        {
            Source = source,
            Status = PermissionStatus.Allowed,
            AllowedActions = new HashSet<SourceAction>(allowedActions),
            EvidenceId = evidenceId,
            Scope = scope,
            RecipientOrigin = NormalizeOrigin(recipientOrigin),
            VerifiedAt = verifiedAt,
            ExpiresAt = expiresAt,
            ReviewAt = reviewAt
        };
    }

    public static SourcePermission LinkedInBlocked() => new()
    {
        Source = SourceKind.LinkedInRestricted,
        Status = PermissionStatus.PermissionRequired,
        Scope = "no-platform-authorization",
        EvidenceId = "none",
        RecipientOrigin = "https://www.linkedin.com"
    };

    private static string NormalizeOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return string.Empty;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Recipient origin must be an absolute HTTP(S) origin.", nameof(origin));
        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}

public static class SourcePermissionPolicy
{
    public static void ValidateForPersistence(SourcePermission permission, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(permission);
        if (permission.Source == SourceKind.LinkedInRestricted &&
            (permission.Status == PermissionStatus.Allowed || permission.AllowedActions.Count != 0))
            throw new InvalidDataException("LinkedIn automation permission has not been verified.");
        if (permission.Status != PermissionStatus.Allowed) return;
        if (string.IsNullOrWhiteSpace(permission.EvidenceId) || string.IsNullOrWhiteSpace(permission.Scope))
            throw new InvalidDataException("Allowed source permission requires evidence and scope metadata.");
        if (permission.VerifiedAt == default || permission.VerifiedAt > now)
            throw new InvalidDataException("Allowed source permission has an invalid verification time.");
        if (permission.ExpiresAt is not null && permission.ExpiresAt <= permission.VerifiedAt)
            throw new InvalidDataException("Source permission expiry must follow verification.");
        if (permission.AllowedActions.Any(action => action != SourceAction.AnalyzeProvidedText) &&
            string.IsNullOrWhiteSpace(permission.RecipientOrigin))
            throw new InvalidDataException("External source actions require a recipient origin.");
    }
}
