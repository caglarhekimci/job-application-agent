using System.Text;
using System.Text.Json;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Answers;

public static class ModelAnswerProposalValidator
{
    private static readonly HashSet<string> Properties = new(StringComparer.Ordinal)
    {
        "schemaVersion", "semanticKey", "proposedValue", "language", "evidenceIds", "rationale"
    };

    public static AnswerProposalValidation ValidateJson(string json, FormQuestion question,
        CandidateProfile profile, IReadOnlyCollection<string> suppliedEvidenceIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(suppliedEvidenceIds);

        if (Encoding.UTF8.GetByteCount(json) > 32 * 1024)
            return Abstain("The model output exceeds the answer proposal size limit.");
        if (question.Sensitive || question.RequiresCandidateAttestation)
            return Abstain("This question requires direct candidate input.");

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Abstain("The model output is not a proposal object.");
            var properties = root.EnumerateObject().ToList();
            if (properties.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != properties.Count)
                return Abstain("The model output contains a duplicate schema key.");
            if (properties.Count != Properties.Count || properties.Any(item => !Properties.Contains(item.Name)))
                return Abstain("The model output does not match answer proposal schema v1.");
            if (!root.TryGetProperty("schemaVersion", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var schemaVersion) ||
                schemaVersion != 1)
                return Abstain("The model output uses an unsupported schema version.");
            if (!TryRequiredString(root, "semanticKey", 200, out var semanticKey) ||
                !TryRequiredString(root, "proposedValue", Math.Min(question.MaxLength ?? 4_000, 4_000),
                    out var proposedValue) ||
                !TryRequiredString(root, "language", 32, out var language) ||
                !TryRequiredString(root, "rationale", 2_000, out var rationale))
                return Abstain("The model output contains a missing or invalid string field.");
            if (!semanticKey.Equals(question.Key, StringComparison.OrdinalIgnoreCase) ||
                !language.Equals(question.Language, StringComparison.OrdinalIgnoreCase))
                return Abstain("The proposal does not match the requested question and language.");
            if (!root.TryGetProperty("evidenceIds", out var evidenceElement) ||
                evidenceElement.ValueKind != JsonValueKind.Array)
                return Abstain("The proposal evidence list is invalid.");
            var evidenceIds = new List<string>();
            foreach (var item in evidenceElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()) ||
                    item.GetString()!.Length > 200 || evidenceIds.Count == 20)
                    return Abstain("The proposal evidence list is invalid.");
                evidenceIds.Add(item.GetString()!);
            }
            if (evidenceIds.Count == 0 || evidenceIds.Distinct(StringComparer.Ordinal).Count() != evidenceIds.Count)
                return Abstain("A grounded proposal requires unique evidence.");

            var supplied = suppliedEvidenceIds.ToHashSet(StringComparer.Ordinal);
            var usable = ProfilePolicy.UsableVerifiedFacts(profile, now)
                .Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
            if (evidenceIds.Any(id => !supplied.Contains(id) || !usable.Contains(id)))
                return Abstain("The proposal cites evidence that was not supplied or is not currently verified.");

            return new()
            {
                Disposition = AnswerProposalDisposition.RequiresReview,
                Reason = "The grounded proposal requires explicit user review before it can be saved.",
                Proposal = new()
                {
                    SchemaVersion = schemaVersion,
                    SemanticKey = semanticKey,
                    ProposedValue = proposedValue,
                    Language = language,
                    EvidenceIds = evidenceIds,
                    Rationale = rationale,
                    ReviewStatus = AnswerProposalReviewStatus.Proposed
                }
            };
        }
        catch (JsonException)
        {
            return Abstain("The model output is not valid JSON.");
        }
    }

    private static bool TryRequiredString(JsonElement root, string name, int maximumLength,
        out string value)
    {
        value = string.Empty;
        if (maximumLength <= 0 || !root.TryGetProperty(name, out var element) ||
            element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
    }

    private static AnswerProposalValidation Abstain(string reason) => new()
    {
        Disposition = AnswerProposalDisposition.Abstained,
        Reason = reason
    };
}
