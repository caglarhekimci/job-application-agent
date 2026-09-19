using System.Globalization;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Answers;

public static class AnswerResolver
{
    public static AnswerResolution Resolve(
        FormQuestion question,
        CandidateProfile profile,
        JobPosting job,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(job);

        if (question.Sensitive)
            return Result(AnswerStatus.ManualOnly, "Sensitive data must be entered by the candidate.");
        if (question.RequiresCandidateAttestation)
            return Result(AnswerStatus.ManualOnly, "Candidate attestation cannot be automated.");

        var deterministic = ResolveDeterministic(question, profile, now);
        if (deterministic is not null)
            return EnforceLength(deterministic, question.MaxLength);

        var scoped = profile.Answers
            .Where(answer => string.Equals(answer.SemanticKey, question.Key, StringComparison.OrdinalIgnoreCase))
            .Where(answer => string.Equals(answer.Language, question.Language, StringComparison.OrdinalIgnoreCase))
            .Where(answer => ScopeMatches(answer, job))
            .OrderByDescending(answer => answer.Scope)
            .ThenByDescending(answer => answer.UpdatedAt)
            .FirstOrDefault();

        if (scoped is null)
            return Result(AnswerStatus.NeedsInput, "No verified answer exists for this question and scope.");
        if (scoped.ExpiresAt is not null && scoped.ExpiresAt <= now)
            return Result(AnswerStatus.RequiresReview, "The stored answer has expired.");
        if (scoped.EvidenceIds.Count > 0)
        {
            var usableEvidence = ProfilePolicy.UsableVerifiedFacts(profile, now)
                .Select(fact => fact.Id)
                .ToHashSet(StringComparer.Ordinal);
            if (scoped.EvidenceIds.Any(evidenceId => !usableEvidence.Contains(evidenceId)))
                return Result(AnswerStatus.RequiresReview, "The stored answer references missing, expired, or unverified evidence.");
        }

        return EnforceLength(new()
        {
            Status = AnswerStatus.Resolved,
            Value = scoped.Answer,
            Reason = "Resolved from scoped answer memory.",
            EvidenceIds = [.. scoped.EvidenceIds]
        }, question.MaxLength);
    }

    private static AnswerResolution? ResolveDeterministic(FormQuestion question, CandidateProfile profile, DateTimeOffset now)
    {
        if (question.Key.Equals("contact.name", StringComparison.OrdinalIgnoreCase))
            return ConfirmedContact(profile, ProfileField.FullName, profile.FullName, "name");
        if (question.Key.Equals("contact.email", StringComparison.OrdinalIgnoreCase))
            return ConfirmedContact(profile, ProfileField.Email, profile.Email, "email");
        if (question.Key.Equals("salary.expected.monthly.net.TRY", StringComparison.OrdinalIgnoreCase))
            return ResolveSalary(profile);
        if (question.Key.StartsWith("salary.current", StringComparison.OrdinalIgnoreCase) ||
            question.Key.StartsWith("salary.expected.", StringComparison.OrdinalIgnoreCase))
            return Result(AnswerStatus.NeedsInput, "No approved value exists for the requested salary semantics.");
        if (question.Key.Equals("experience.professional.csharp.years", StringComparison.OrdinalIgnoreCase))
            return ResolveExperience(profile, "C#", now);
        return null;
    }

    private static AnswerResolution ConfirmedContact(CandidateProfile profile, ProfileField field, string value, string label) =>
        ProfilePolicy.IsLocallyConfirmed(profile, field) && !string.IsNullOrWhiteSpace(value)
            ? new() { Status = AnswerStatus.Resolved, Value = value, Reason = $"Locally confirmed {label}." }
            : Result(AnswerStatus.RequiresReview, $"The {label} requires local confirmation.");

    private static AnswerResolution ResolveSalary(CandidateProfile profile)
    {
        var target = profile.Salary.Target;
        if (!ProfilePolicy.IsLocallyConfirmed(profile, ProfileField.Salary) || profile.Salary.ConfirmedAt is null)
            return Result(AnswerStatus.RequiresReview, "Salary requires local confirmation.");
        if (target.Period != SalaryPeriod.Month || target.TaxBasis != TaxBasis.Net ||
            !target.Currency.Equals("TRY", StringComparison.OrdinalIgnoreCase))
            return Result(AnswerStatus.NeedsInput, "The stored salary unit does not match the form.");

        return new()
        {
            Status = AnswerStatus.Resolved,
            Value = target.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            Reason = "Locally confirmed expected monthly net salary in TRY."
        };
    }

    private static AnswerResolution ResolveExperience(CandidateProfile profile, string skill, DateTimeOffset now)
    {
        var verifiedIds = ProfilePolicy.UsableVerifiedFacts(profile, now).Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);
        var sourcePeriods = profile.Experience
            .Where(period => period.Kind == ExperienceKind.Professional)
            .Where(period => period.Skills.Any(item => item.Equals(skill, StringComparison.OrdinalIgnoreCase)))
            .Where(period => period.EvidenceIds.Any(verifiedIds.Contains))
            .Where(period => period.Start < currentDate)
            .ToList();
        var periods = sourcePeriods
            .Select(period => (period.Start, End: period.End is null || period.End > currentDate ? currentDate : period.End.Value))
            .OrderBy(period => period.Start)
            .ToList();

        if (periods.Count == 0)
            return Result(AnswerStatus.NeedsInput, "No verified professional experience exists for this skill.");

        var merged = new List<(DateOnly Start, DateOnly End)>();
        foreach (var period in periods)
        {
            if (merged.Count == 0 || period.Start > merged[^1].End.AddDays(1))
                merged.Add(period);
            else if (period.End > merged[^1].End)
                merged[^1] = (merged[^1].Start, period.End);
        }

        var days = merged.Sum(period => period.End.DayNumber - period.Start.DayNumber);
        var years = Math.Floor(days / 365m);
        return new()
        {
            Status = AnswerStatus.Resolved,
            Value = years.ToString(CultureInfo.InvariantCulture),
            Reason = "Calculated from non-overlapping verified professional periods.",
            EvidenceIds = sourcePeriods.SelectMany(item => item.EvidenceIds)
                .Where(verifiedIds.Contains).Distinct(StringComparer.Ordinal).ToList()
        };
    }

    private static bool ScopeMatches(AnswerMemory answer, JobPosting job) => answer.Scope switch
    {
        AnswerScopeType.Default => true,
        AnswerScopeType.Company => string.Equals(answer.ScopeId, job.Employer, StringComparison.OrdinalIgnoreCase),
        AnswerScopeType.Application => string.Equals(answer.ScopeId, job.Id, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private static AnswerResolution EnforceLength(AnswerResolution result, int? maxLength) =>
        maxLength is not null && result.Value?.Length > maxLength
            ? Result(AnswerStatus.RequiresReview, "The verified answer exceeds the field length limit.")
            : result;

    private static AnswerResolution Result(AnswerStatus status, string reason) => new() { Status = status, Reason = reason };
}
