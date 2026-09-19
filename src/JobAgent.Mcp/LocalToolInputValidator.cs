using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobAgent.Infrastructure.Workspace;

namespace JobAgent.Mcp;

public static class LocalToolInputValidator
{
    public const int MaximumBytes = 262144;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static bool IsValid(string? name, IDictionary<string, JsonElement>? arguments)
    {
        string[]? allowed = name switch
        {
            "runtime_get_capabilities" => [],
            "profile_get_summary" => ["profileRef"],
            "profile_propose_patch" => ["profileRef", "baseVersion", "changes"],
            "job_import_text" => ["text", "sourceUrl", "employer", "title"],
            "job_evaluate" => ["jobRef", "profileRef"],
            "application_create_draft" => ["jobRef", "profileRef", "resumeRef"],
            "application_propose_answers" => ["applicationRef", "baseRevision", "answers", "evidenceRefs"],
            "application_get_questions" or "application_prepare_review" or "application_execute_approved"
                or "application_get_status" or "application_cancel" => ["applicationRef"],
            _ => null
        };
        if (allowed is null || arguments?.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)) == true)
            return false;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(arguments ?? new Dictionary<string, JsonElement>());
            if (bytes.Length > MaximumBytes) return false;
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = 8 });
            if (!UniqueKeys(document.RootElement)) return false;
            foreach (var property in document.RootElement.EnumerateObject())
                if (property.Name.EndsWith("Ref", StringComparison.Ordinal) &&
                    (property.Value.ValueKind != JsonValueKind.String ||
                     !Guid.TryParseExact(property.Value.GetString(), "D", out var id) || id == Guid.Empty)) return false;
            return name switch
            {
                "profile_propose_patch" => Valid(JsonSerializer.Deserialize<HostProfilePatchRequest>(bytes, Json)!),
                "job_import_text" => Valid(JsonSerializer.Deserialize<HostJobImportRequest>(bytes, Json)!),
                "application_propose_answers" => Valid(JsonSerializer.Deserialize<HostAnswerProposalRequest>(bytes, Json)!),
                _ => true
            };
        }
        catch (Exception e) when (e is JsonException or ArgumentException or NotSupportedException) { return false; }
    }

    public static bool UniqueKeys(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count()
            == element.EnumerateObject().Count() && element.EnumerateObject().All(p => UniqueKeys(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().All(UniqueKeys),
        _ => true
    };

    public static bool Valid(HostProfilePatchRequest r) => r is not null && r.ProfileRef != Guid.Empty && r.BaseVersion > 0 &&
        r.Changes is { Count: > 0 and <= 20 } && r.Changes.All(c => c is not null && Enum.IsDefined(c.Field) &&
            Text(c.Value, c.Field switch
            {
                HostProfileField.Locale => 32,
                HostProfileField.ProfessionalSkill => 80,
                HostProfileField.FullName => 200,
                _ => 254
            }) && References(c.EvidenceRefs) &&
            (c.Field != HostProfileField.ProfessionalSkill || c.EvidenceRefs.Count > 0));
    public static bool Valid(HostJobImportRequest r) => r is not null && Text(r.Text, 100000) &&
        OptionalText(r.Employer, 200) && OptionalText(r.Title, 300) &&
        (string.IsNullOrEmpty(r.SourceUrl) || r.SourceUrl.Length <= 2048 &&
            Uri.TryCreate(r.SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0);
    public static bool Valid(HostAnswerProposalRequest r) => r is not null && r.ApplicationRef != Guid.Empty && r.BaseRevision >= 0 &&
        References(r.EvidenceRefs) && r.Answers is { Count: > 0 and <= 20 } &&
        r.Answers.Select(a => a?.SemanticKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Answers.Count &&
        r.Answers.All(a => a is not null && a.SchemaVersion == 1 && Text(a.SemanticKey, 200) &&
            Text(a.ProposedValue, 4000) && Text(a.Language, 32) && Text(a.Rationale, 2000) && References(a.EvidenceIds));
    public static bool References(IReadOnlyList<string>? values) => values is { Count: <= 20 } &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count && values.All(v => Text(v, 200) &&
            v.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
    public static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    private static bool OptionalText(string? value, int maximum) => value is null || value.Length <= maximum;
}
