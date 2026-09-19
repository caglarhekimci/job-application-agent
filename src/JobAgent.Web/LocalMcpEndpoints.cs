using System.Text.Json;
using System.Text.Json.Serialization;
using JobAgent.Core.Applications;
using JobAgent.Infrastructure.Workspace;

namespace JobAgent.Web;

public static class LocalMcpEndpoints
{
    public const string Prefix = "/internal/mcp/local/v1";
    public const int MaximumBytes = 262144;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static bool TryRoute(PathString path, out bool allowsBody)
    {
        allowsBody = false;
        if (!path.StartsWithSegments(Prefix, out var remaining)) return false;
        var parts = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (remaining.Value != "/" + string.Join('/', parts)) return false;
        if (parts is ["refs"]) return true;
        if (parts is ["jobs", "import"] or ["applications"]) { allowsBody = true; return true; }
        if (parts.Length != 3 || !Guid.TryParseExact(parts[1], "D", out var reference) || reference == Guid.Empty) return false;
        allowsBody = (parts[0], parts[2]) is ("profiles", "patches") or ("jobs", "evaluate") or ("applications", "answers");
        return allowsBody || (parts[0], parts[2]) is ("profiles", "summary") or
            ("applications", "questions" or "review" or "execute" or "status" or "cancel");
    }

    public static void Map(WebApplication app)
    {
        app.MapPost(Prefix + "/refs", async (ILocalWorkspaceHost w) => Result(await w.GetHostWorkspaceRefsAsync()));
        app.MapPost(Prefix + "/profiles/{profileRef:guid}/summary", async (ILocalWorkspaceHost w, Guid profileRef) =>
            Result(await w.GetHostProfileSummaryAsync(profileRef)));
        app.MapPost(Prefix + "/profiles/{profileRef:guid}/patches", async (ILocalWorkspaceHost w, Guid profileRef, HttpContext c) =>
        {
            var request = await ReadAsync<HostProfilePatchRequest>(c);
            if (request.ProfileRef != profileRef) throw new PolicyException("ReferenceMismatch");
            return Result(await w.ProposeProfilePatchAsync(request));
        });
        app.MapPost(Prefix + "/jobs/import", async (ILocalWorkspaceHost w, HttpContext c) =>
            Result(await w.ImportJobProposalAsync(await ReadAsync<HostJobImportRequest>(c))));
        app.MapPost(Prefix + "/jobs/{jobRef:guid}/evaluate", async (ILocalWorkspaceHost w, Guid jobRef, HttpContext c) =>
            Result(await w.EvaluateJobAsync(jobRef, (await ReadAsync<EvaluateRequest>(c)).ProfileRef)));
        app.MapPost(Prefix + "/applications", async (ILocalWorkspaceHost w, HttpContext c) =>
            Result(await w.CreateApplicationAsync(await ReadAsync<HostCreateApplicationRequest>(c))));
        app.MapPost(Prefix + "/applications/{applicationRef:guid}/questions", async (ILocalWorkspaceHost w, Guid applicationRef) =>
            Result(await w.GetApplicationQuestionsAsync(applicationRef)));
        app.MapPost(Prefix + "/applications/{applicationRef:guid}/answers", async (ILocalWorkspaceHost w, Guid applicationRef, HttpContext c) =>
        {
            var request = await ReadAsync<HostAnswerProposalRequest>(c);
            if (request.ApplicationRef != applicationRef) throw new PolicyException("ReferenceMismatch");
            return Result(await w.ProposeApplicationAnswersAsync(request));
        });
        app.MapPost(Prefix + "/applications/{applicationRef:guid}/review", async (ILocalWorkspaceHost w, Guid applicationRef) =>
            Result(await w.PrepareApplicationReviewAsync(applicationRef)));
        app.MapPost(Prefix + "/applications/{applicationRef:guid}/execute", async (ILocalWorkspaceHost w, Guid applicationRef) =>
            Result(await w.ExecuteApprovedApplicationAsync(applicationRef)));
        app.MapPost(Prefix + "/applications/{applicationRef:guid}/status", async (ILocalWorkspaceHost w, Guid applicationRef) =>
            Result(await w.GetApplicationStatusAsync(applicationRef)));
        app.MapPost(Prefix + "/applications/{applicationRef:guid}/cancel", async (ILocalWorkspaceHost w, Guid applicationRef) =>
            Result(await w.CancelApplicationAsync(applicationRef)));
    }

    private static IResult Result<T>(T result)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result, Json);
        if (bytes.Length > MaximumBytes) throw new PolicyException("ResponseTooLarge");
        return Results.Bytes(bytes, "application/json");
    }
    private static async Task<T> ReadAsync<T>(HttpContext context)
    {
        try
        {
            using var stream = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(chunk, context.RequestAborted)) > 0)
            {
                if (stream.Length + count > MaximumBytes) throw new PolicyException("InvalidArguments");
                stream.Write(chunk, 0, count);
            }
            using var doc = JsonDocument.Parse(stream.ToArray(), new() { MaxDepth = 8 });
            if (!ValidJson(doc.RootElement)) throw new PolicyException("InvalidArguments");
            var value = doc.RootElement.Deserialize<T>(Json);
            if (value is null || !ValidInput(value)) throw new PolicyException("InvalidArguments");
            return value;
        }
        catch (JsonException) { throw new PolicyException("InvalidArguments"); }
    }
    private static bool ValidJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array) return element.EnumerateArray().All(ValidJson);
        if (element.ValueKind != JsonValueKind.Object) return true;
        var properties = element.EnumerateObject().ToArray();
        return properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == properties.Length &&
            properties.All(p => ValidJson(p.Value) && (!p.Name.EndsWith("Ref", StringComparison.Ordinal) ||
                p.Value.ValueKind == JsonValueKind.String && Guid.TryParseExact(p.Value.GetString(), "D", out var id) && id != Guid.Empty));
    }
    private static bool ValidInput<T>(T value) => value switch
    {
        HostProfilePatchRequest r => r.ProfileRef != Guid.Empty && r.BaseVersion > 0 && r.Changes is { Count: > 0 and <= 20 } &&
            r.Changes.All(c => c is not null && Enum.IsDefined(c.Field) && Text(c.Value, 254) && Refs(c.EvidenceRefs)),
        HostJobImportRequest r => Text(r.Text, 100000) && r.SourceUrl?.Length is not > 2048 &&
            r.Employer?.Length is not > 200 && r.Title?.Length is not > 300,
        HostCreateApplicationRequest r => r.JobRef != Guid.Empty && r.ProfileRef != Guid.Empty && r.ResumeRef != Guid.Empty,
        HostAnswerProposalRequest r => r.ApplicationRef != Guid.Empty && r.BaseRevision >= 0 && Refs(r.EvidenceRefs) &&
            r.Answers is { Count: > 0 and <= 20 } && r.Answers.All(a => a is not null && a.SchemaVersion == 1 &&
                Text(a.SemanticKey, 200) && Text(a.ProposedValue, 4000) && Text(a.Language, 32) && Text(a.Rationale, 2000) && Refs(a.EvidenceIds)),
        EvaluateRequest r => r.ProfileRef != Guid.Empty,
        _ => false
    };
    private static bool Refs(IReadOnlyList<string>? values) => values is { Count: <= 20 } &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count && values.All(v => Text(v, 200) &&
            v.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
    private static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
    private sealed record EvaluateRequest(Guid ProfileRef);
}
