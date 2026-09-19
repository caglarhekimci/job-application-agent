namespace JobAgent.Core.Profiles;

public static class ProfilePolicy
{
    public static IReadOnlyList<EvidenceFact> UsableVerifiedFacts(CandidateProfile profile, DateTimeOffset now) =>
        profile.Facts
            .Where(fact => fact.VerificationStatus == VerificationStatus.Verified)
            .Where(fact => fact.ValidFrom is null || fact.ValidFrom <= now)
            .Where(fact => fact.ValidUntil is null || fact.ValidUntil > now)
            .ToList();

    public static bool IsLocallyConfirmed(CandidateProfile profile, ProfileField field) =>
        profile.LocalConfirmations.Any(confirmation => confirmation.Field == field);

    public static CandidateProfile ConfirmLocally(
        CandidateProfile profile,
        DateTimeOffset confirmedAt,
        params ProfileField[] fields)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(fields);
        var selected = fields.ToHashSet();
        var retained = profile.LocalConfirmations.Where(item => !selected.Contains(item.Field));
        var confirmations = retained
            .Concat(selected.Select(field => new LocalConfirmation { Field = field, ConfirmedAt = confirmedAt }))
            .OrderBy(item => item.Field)
            .ToList();
        return profile with { LocalConfirmations = confirmations };
    }
}
