using JobAgent.Core.Profiles;

namespace JobAgent.Core.Answers;

public sealed record ReviewedAnswerMemoryUpdate
{
    public string SemanticKey { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public AnswerScopeType Scope { get; init; }
    public string? ScopeId { get; init; }
    public string Language { get; init; } = "en";
    public List<string> EvidenceIds { get; init; } = [];
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record AnswerMemoryKey
{
    public string SemanticKey { get; init; } = string.Empty;
    public AnswerScopeType Scope { get; init; }
    public string? ScopeId { get; init; }
    public string Language { get; init; } = "en";
}

public static class AnswerMemoryService
{
    public static CandidateProfile UpsertReviewed(CandidateProfile profile,
        ReviewedAnswerMemoryUpdate update, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(update);
        var key = NormalizeKey(update.SemanticKey, update.Language, update.Scope, update.ScopeId);
        if (string.IsNullOrWhiteSpace(update.Answer) || update.Answer.Length > 4_000)
            throw new InvalidDataException("A reviewed answer is required and must be at most 4000 characters.");
        if (update.ExpiresAt is not null && update.ExpiresAt <= now)
            throw new InvalidDataException("A reviewed answer expiry must be in the future.");
        if (update.EvidenceIds is null || update.EvidenceIds.Count > 20)
            throw new InvalidDataException("A reviewed answer may reference at most 20 evidence records.");
        var evidenceIds = update.EvidenceIds.Distinct(StringComparer.Ordinal).ToList();
        var usable = ProfilePolicy.UsableVerifiedFacts(profile, now)
            .Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        if (evidenceIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 200 || !usable.Contains(id)))
            throw new InvalidDataException("Reviewed answer evidence must be currently verified.");

        var answers = profile.Answers.Where(item => !Matches(item, key)).ToList();
        answers.Add(new()
        {
            SemanticKey = key.SemanticKey,
            Answer = update.Answer.Trim(),
            Scope = key.Scope,
            ScopeId = key.ScopeId,
            Language = key.Language,
            EvidenceIds = evidenceIds,
            UpdatedAt = now,
            ExpiresAt = update.ExpiresAt
        });
        return profile with { Version = checked(profile.Version + 1), Answers = answers };
    }

    public static CandidateProfile Revoke(CandidateProfile profile, AnswerMemoryKey key)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(key);
        var normalized = NormalizeKey(key.SemanticKey, key.Language, key.Scope, key.ScopeId);
        var answers = profile.Answers.Where(item => !Matches(item, normalized)).ToList();
        return answers.Count == profile.Answers.Count
            ? profile
            : profile with { Version = checked(profile.Version + 1), Answers = answers };
    }

    private static AnswerMemoryKey NormalizeKey(string semanticKey, string language,
        AnswerScopeType scope, string? scopeId)
    {
        if (string.IsNullOrWhiteSpace(semanticKey) || semanticKey.Length > 200 ||
            string.IsNullOrWhiteSpace(language) || language.Length > 32 || !Enum.IsDefined(scope))
            throw new InvalidDataException("Answer memory key is invalid.");
        var normalizedScopeId = string.IsNullOrWhiteSpace(scopeId) ? null : scopeId.Trim();
        if (scope == AnswerScopeType.Default && normalizedScopeId is not null ||
            scope != AnswerScopeType.Default && normalizedScopeId is null || normalizedScopeId?.Length > 300)
            throw new InvalidDataException("Answer memory scope is invalid.");
        return new()
        {
            SemanticKey = semanticKey.Trim(),
            Language = language.Trim(),
            Scope = scope,
            ScopeId = normalizedScopeId
        };
    }

    private static bool Matches(AnswerMemory item, AnswerMemoryKey key) =>
        item.Scope == key.Scope &&
        string.Equals(item.SemanticKey, key.SemanticKey, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.Language, key.Language, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.ScopeId, key.ScopeId, StringComparison.OrdinalIgnoreCase);
}
