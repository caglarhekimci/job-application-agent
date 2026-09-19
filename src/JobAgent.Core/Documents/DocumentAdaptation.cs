using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Documents;

public enum AdaptedDocumentKind { Resume, CoverLetter }
public enum AdaptationChangeKind { Included, Moved, Omitted, Structural }

public sealed record AdaptationSourceSegment(string SourceSpan, string Text);

public sealed record DocumentAdaptationInput
{
    public Guid ApplicationRef { get; init; }
    public string ApplicationPayloadHash { get; init; } = string.Empty;
    public CandidateProfile Profile { get; init; } = new();
    public Guid ResumeRef { get; init; }
    public string ResumeHash { get; init; } = string.Empty;
    public string SourceDocumentId { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public IReadOnlyList<AdaptationSourceSegment> SourceSegments { get; init; } = [];
    public Guid JobRef { get; init; }
    public JobPosting Job { get; init; } = new();
}

public sealed record DocumentAdaptationBinding(Guid ApplicationRef, string ApplicationPayloadHash,
    Guid ProfileId, int ProfileVersion, Guid ResumeRef, string ResumeHash, Guid JobRef,
    string JobBindingHash, string SourceFactsHash);

public sealed record AdaptationCitation(string EvidenceId, string SourceDocumentId, string SourceSpan,
    ExperienceKind ExperienceKind, string ExactText, int OutputOrdinal);

public sealed record AdaptationArtifact(AdaptedDocumentKind Kind, string Content, string ContentHash,
    IReadOnlyList<AdaptationCitation> Citations);

public sealed record AdaptationChange(AdaptationChangeKind Kind, string Text, string? SourceSpan,
    int? OriginalOrdinal, int? ProposedOrdinal);

public sealed record DocumentAdaptationProposal(Guid Id, DocumentAdaptationBinding Binding,
    string BindingHash, string BundleHash, AdaptationArtifact Resume, AdaptationArtifact CoverLetter,
    IReadOnlyList<AdaptationChange> Changes, DateTimeOffset CreatedAt);

public sealed class DocumentAdaptationException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public static class DocumentAdapter
{
    private const int MaximumArtifactCharacters = 100_000;
    private const int MaximumCoverStatements = 3;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ExperienceKind[] KindOrder =
    [
        ExperienceKind.Professional,
        ExperienceKind.Internship,
        ExperienceKind.PartTime,
        ExperienceKind.PersonalProject
    ];

    public static DocumentAdaptationProposal Create(DocumentAdaptationInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);
        var segments = input.SourceSegments
            .Select((segment, index) => new IndexedSegment(segment, index))
            .ToArray();
        if (segments.Select(item => item.Segment.SourceSpan).Distinct(StringComparer.Ordinal).Count() != segments.Length)
            throw new DocumentAdaptationException("AdaptationEvidenceInvalid");
        var segmentsBySpan = segments.ToDictionary(item => item.Segment.SourceSpan, StringComparer.Ordinal);
        var usableFacts = ProfilePolicy.UsableVerifiedFacts(input.Profile, now);
        var statements = new List<Statement>();
        foreach (var fact in usableFacts)
        {
            if (!string.Equals(fact.SourceDocumentId, input.SourceDocumentId, StringComparison.Ordinal) ||
                !segmentsBySpan.TryGetValue(fact.SourceSpan, out var source) ||
                !string.Equals(fact.Value, source.Segment.Text, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(fact.Value))
                throw new DocumentAdaptationException("AdaptationEvidenceInvalid");
            var periods = input.Profile.Experience
                .Where(period => period.EvidenceIds.Contains(fact.Id, StringComparer.Ordinal)).ToArray();
            var kinds = periods.Select(period => period.Kind).Distinct().ToArray();
            if (periods.Length == 0 || kinds.Length != 1 ||
                !string.Equals(fact.Kind, kinds[0].ToString(), StringComparison.Ordinal))
                throw new DocumentAdaptationException("AdaptationEvidenceInvalid");
            statements.Add(new(fact, kinds[0], periods.SelectMany(period => period.Skills)
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), source.Index));
        }
        if (statements.Count == 0) throw new DocumentAdaptationException("AdaptationEvidenceRequired");
        if (statements.GroupBy(statement => statement.Fact.SourceSpan, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new DocumentAdaptationException("AdaptationEvidenceInvalid");

        var reviewedSkills = input.Job.Requirements
            .Where(requirement => requirement.ReviewStatus == RequirementReviewStatus.Confirmed)
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.Skill))
            .Select(requirement => new ReviewedSkill(requirement.Skill!,
                requirement.Importance == RequirementImportance.Mandatory ? 2 : 1)).ToArray();
        var ranked = statements
            .OrderByDescending(statement => Score(statement, reviewedSkills))
            .ThenBy(statement => statement.SourceOrdinal)
            .ThenBy(statement => statement.Fact.Id, StringComparer.Ordinal)
            .ToArray();

        var resume = BuildResume(input.Job, ranked);
        var coverLetter = BuildCoverLetter(input.Job, ranked);
        if (resume.Content.Length > MaximumArtifactCharacters || coverLetter.Content.Length > MaximumArtifactCharacters)
            throw new DocumentAdaptationException("AdaptationOutputLimitExceeded");

        var jobBindingHash = Hash(new
        {
            input.JobRef,
            input.Job.Id,
            input.Job.Title,
            input.Job.Employer,
            input.Job.TextHash,
            input.Job.CanonicalUrl,
            Requirements = input.Job.Requirements
                .OrderBy(requirement => requirement.Id, StringComparer.Ordinal)
                .Select(requirement => new
                {
                    requirement.Id,
                    requirement.RequirementText,
                    requirement.Type,
                    requirement.Importance,
                    requirement.Skill,
                    requirement.MinimumYears,
                    requirement.ExpectedValue,
                    requirement.SourceSpan,
                    requirement.ReviewStatus
                }).ToArray()
        });
        var sourceFactsHash = Hash(ranked.OrderBy(statement => statement.Fact.Id, StringComparer.Ordinal)
            .Select(statement => new
            {
                statement.Fact.Id,
                statement.Fact.SourceDocumentId,
                statement.Fact.SourceSpan,
                statement.Fact.Value,
                statement.Fact.ValidFrom,
                statement.Fact.ValidUntil,
                statement.ExperienceKind,
                statement.Skills
            }).ToArray());
        var binding = new DocumentAdaptationBinding(input.ApplicationRef, input.ApplicationPayloadHash,
            input.Profile.Id, input.Profile.Version, input.ResumeRef, input.ResumeHash, input.JobRef,
            jobBindingHash, sourceFactsHash);
        var bindingHash = Hash(binding);
        var bundleHash = Hash(new { BindingHash = bindingHash, resume.ContentHash, CoverLetterHash = coverLetter.ContentHash });
        return new(Guid.NewGuid(), binding, bindingHash, bundleHash, resume, coverLetter,
            BuildChanges(segments, resume.Citations), now);
    }

    private static void ValidateInput(DocumentAdaptationInput input)
    {
        if (input.ApplicationRef == Guid.Empty || input.Profile.Id == Guid.Empty || input.Profile.Version <= 0 ||
            input.Profile.VerifiedAt is null || input.ResumeRef == Guid.Empty || input.JobRef == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.ApplicationPayloadHash) || string.IsNullOrWhiteSpace(input.ResumeHash) ||
            string.IsNullOrWhiteSpace(input.SourceDocumentId) || input.SourceSegments.Count == 0 ||
            input.Job.RequirementsReviewedAt is null || string.IsNullOrWhiteSpace(input.Job.Title) ||
            string.IsNullOrWhiteSpace(input.Job.Employer))
            throw new DocumentAdaptationException("AdaptationSourcesNotReviewed");
    }

    private static AdaptationArtifact BuildResume(JobPosting job, IReadOnlyList<Statement> ranked)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Adapted CV for {SafeLabel(job.Title)} at {SafeLabel(job.Employer)}");
        builder.AppendLine("Assembled only from exact reviewed CV evidence.");
        var citations = new List<AdaptationCitation>();
        var ordinal = 0;
        foreach (var kind in KindOrder)
        {
            var items = ranked.Where(statement => statement.ExperienceKind == kind).ToArray();
            if (items.Length == 0) continue;
            builder.AppendLine().AppendLine(Heading(kind));
            foreach (var item in items)
            {
                builder.AppendLine(item.Fact.Value);
                citations.Add(Citation(item, ordinal++));
            }
        }
        var content = builder.ToString().TrimEnd() + Environment.NewLine;
        return new(AdaptedDocumentKind.Resume, content, HashText(content), citations);
    }

    private static AdaptationArtifact BuildCoverLetter(JobPosting job, IReadOnlyList<Statement> ranked)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Application for {SafeLabel(job.Title)} at {SafeLabel(job.Employer)}");
        builder.AppendLine().AppendLine("Relevant evidence from my reviewed CV:");
        var citations = ranked.Take(MaximumCoverStatements).Select((item, index) => Citation(item, index)).ToArray();
        foreach (var citation in citations) builder.AppendLine(citation.ExactText);
        var content = builder.ToString().TrimEnd() + Environment.NewLine;
        return new(AdaptedDocumentKind.CoverLetter, content, HashText(content), citations);
    }

    private static IReadOnlyList<AdaptationChange> BuildChanges(IReadOnlyList<IndexedSegment> segments,
        IReadOnlyList<AdaptationCitation> citations)
    {
        var proposedBySpan = citations.ToDictionary(citation => citation.SourceSpan,
            citation => citation.OutputOrdinal, StringComparer.Ordinal);
        var includedInSourceOrder = segments.Where(item => proposedBySpan.ContainsKey(item.Segment.SourceSpan))
            .Select((item, index) => new { item.Segment.SourceSpan, SelectedOrdinal = index }).ToDictionary(
                item => item.SourceSpan, item => item.SelectedOrdinal, StringComparer.Ordinal);
        var changes = new List<AdaptationChange>
        {
            new(AdaptationChangeKind.Structural, "Fixed job header and evidence section headings", null, null, null)
        };
        changes.AddRange(segments.Select(item =>
        {
            if (!proposedBySpan.TryGetValue(item.Segment.SourceSpan, out var proposed))
                return new AdaptationChange(AdaptationChangeKind.Omitted, item.Segment.Text,
                    item.Segment.SourceSpan, item.Index, null);
            var kind = includedInSourceOrder[item.Segment.SourceSpan] == proposed
                ? AdaptationChangeKind.Included : AdaptationChangeKind.Moved;
            return new AdaptationChange(kind, item.Segment.Text, item.Segment.SourceSpan, item.Index, proposed);
        }));
        return changes;
    }

    private static AdaptationCitation Citation(Statement item, int ordinal) => new(item.Fact.Id,
        item.Fact.SourceDocumentId, item.Fact.SourceSpan, item.ExperienceKind, item.Fact.Value, ordinal);

    private static int Score(Statement statement, IReadOnlyList<ReviewedSkill> skills) => skills.Sum(required =>
        statement.Skills.Contains(required.Skill, StringComparer.OrdinalIgnoreCase) ? required.Weight : 0);

    private static string SafeLabel(string value) => string.Join(' ', value.Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Heading(ExperienceKind kind) => kind switch
    {
        ExperienceKind.Professional => "Professional experience",
        ExperienceKind.Internship => "Internship experience",
        ExperienceKind.PartTime => "Part-time experience",
        ExperienceKind.PersonalProject => "Personal projects",
        _ => throw new DocumentAdaptationException("AdaptationEvidenceInvalid")
    };

    private static string Hash(object value) => HashText(JsonSerializer.Serialize(value, Json));
    private static string HashText(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record IndexedSegment(AdaptationSourceSegment Segment, int Index);
    private sealed record Statement(EvidenceFact Fact, ExperienceKind ExperienceKind,
        IReadOnlyList<string> Skills, int SourceOrdinal);
    private sealed record ReviewedSkill(string Skill, int Weight);
}
