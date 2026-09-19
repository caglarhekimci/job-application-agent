using JobAgent.Core.Permissions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace JobAgent.Core.Jobs;

public static class JobPostingImporter
{
    private static readonly Regex ExperiencePattern = new(
        @"^(?<importance>Mandatory|Required|Preferred):\s*(?<years>\d+(?:\.\d+)?)\s+years?\s+of\s+professional\s+(?<skill>[A-Za-z0-9#.+-]+)\s+experience\.?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static JobPosting ImportProvidedText(string id, string employer, string title, string text, string sourceUrl)
        => ImportProvidedText(id, employer, title, text, sourceUrl, DateTimeOffset.UtcNow);

    public static JobPosting ImportProvidedText(string id, string employer, string title, string text,
        string sourceUrl, DateTimeOffset observedAt, bool synthetic = false)
        => ProposeProvidedText(id, employer, title, text, sourceUrl, observedAt, synthetic).Posting;

    public static JobImportProposal ProposeProvidedText(string id, string employer, string title,
        string text, string sourceUrl, DateTimeOffset observedAt, bool synthetic = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Posting text is required.", nameof(text));
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(employer) || string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Job id, employer and title are required.");
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var normalizedLines = lines.Select(line => Regex.Replace(line.Trim(), @"[ \t]+", " ")).ToArray();
        var normalizedText = string.Join('\n', normalizedLines).Trim();
        var canonicalUrl = JobUrlCanonicalizer.Normalize(sourceUrl);
        var suggestions = new List<JobRequirement>();
        for (var index = 0; index < normalizedLines.Length; index++)
        {
            var match = ExperiencePattern.Match(normalizedLines[index]);
            if (!match.Success) continue;
            var importance = match.Groups["importance"].Value.Equals("Preferred", StringComparison.OrdinalIgnoreCase)
                ? RequirementImportance.Preferred : RequirementImportance.Mandatory;
            suggestions.Add(new()
            {
                Id = $"requirement-line-{index + 1}",
                RequirementText = normalizedLines[index],
                Type = RequirementType.ProfessionalExperienceYears,
                Importance = importance,
                Skill = match.Groups["skill"].Value,
                MinimumYears = decimal.Parse(match.Groups["years"].Value, CultureInfo.InvariantCulture),
                SourceSpan = $"line {index + 1}",
                RuleRationale = "Explicit professional-experience duration pattern in provided text.",
                ReviewStatus = RequirementReviewStatus.Suggested
            });
        }

        return new()
        {
            Posting = new()
            {
                Id = id.Trim(),
                ExternalId = id.Trim(),
                Employer = employer.Trim(),
                Title = title.Trim(),
                Text = normalizedText,
                SourceUrl = sourceUrl.Trim(),
                CanonicalUrl = canonicalUrl,
                TextHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText))),
                FirstSeenAt = observedAt,
                LastCheckedAt = observedAt,
                Synthetic = synthetic,
                SourcePermission = SourcePermission.ForUserProvidedText(observedAt,
                    JobUrlCanonicalizer.Origin(canonicalUrl))
            },
            SuggestedRequirements = suggestions
        };
    }

    public static JobPosting ConfirmRequirements(JobImportProposal proposal,
        IEnumerable<string> acceptedSuggestionIds, DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(acceptedSuggestionIds);
        var accepted = acceptedSuggestionIds.ToList();
        if (accepted.Count != accepted.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException("Accepted requirement ids must be unique.");
        var byId = proposal.SuggestedRequirements.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var confirmed = new List<JobRequirement>();
        foreach (var id in accepted)
        {
            if (!byId.TryGetValue(id, out var suggestion))
                throw new InvalidDataException($"Unknown requirement suggestion: {id}.");
            JobRequirementPolicy.Validate(suggestion);
            confirmed.Add(suggestion with { ReviewStatus = RequirementReviewStatus.Confirmed });
        }
        return ConfirmReviewedRequirements(proposal, confirmed, reviewedAt);
    }

    public static JobPosting ConfirmReviewedRequirements(JobImportProposal proposal,
        IReadOnlyList<JobRequirement> reviewedRequirements, DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(reviewedRequirements);
        if (reviewedRequirements.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count()
            != reviewedRequirements.Count)
            throw new InvalidDataException("Reviewed requirement ids must be unique.");

        var confirmed = new List<JobRequirement>(reviewedRequirements.Count);
        foreach (var requirement in reviewedRequirements)
        {
            JobRequirementPolicy.Validate(requirement);
            if (!proposal.Posting.Text.Contains(requirement.RequirementText, StringComparison.Ordinal))
                throw new InvalidDataException(
                    $"Reviewed requirement '{requirement.Id}' is not present in the supplied job text.");
            confirmed.Add(requirement with { ReviewStatus = RequirementReviewStatus.Confirmed });
        }

        return proposal.Posting with { Requirements = confirmed, RequirementsReviewedAt = reviewedAt };
    }
}
