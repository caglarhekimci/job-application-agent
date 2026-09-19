using System.Text.Json;
using JobAgent.Core.Models;
using JobAgent.Infrastructure.Workspace;

namespace JobAgent.Web;

public static class ModelPolicyEndpoints
{
    private const int MaximumRequestBytes = 4096;
    private static readonly string[] Properties =
    ["expectedRevision", "mode", "maxAnswerProposalOperationsPerApplication"];

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/workspace/model-policy", (LocalWorkspace workspace) => workspace.GetModelPolicyAsync());
        app.MapPost("/api/workspace/model-policy", async (LocalWorkspace workspace, HttpContext context) =>
        {
            var request = await ReadAsync(context);
            if (request is null) return Results.BadRequest(new { error = "InvalidModelPolicyPayload" });
            return Results.Ok(await workspace.UpdateModelPolicyFromUiAsync(request));
        });
    }

    private static async Task<WorkspaceModelPolicyUpdate?> ReadAsync(HttpContext context)
    {
        if (context.Request.ContentLength is > MaximumRequestBytes) return null;
        try
        {
            using var stream = new MemoryStream();
            var buffer = new byte[1024];
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                if (stream.Length + count > MaximumRequestBytes) return null;
                stream.Write(buffer, 0, count);
            }
            using var document = JsonDocument.Parse(stream.ToArray(), new JsonDocumentOptions { MaxDepth = 2 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var properties = root.EnumerateObject().ToArray();
            if (properties.Length != Properties.Length ||
                properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length ||
                properties.Any(property => !Properties.Contains(property.Name, StringComparer.Ordinal))) return null;
            if (!root.TryGetProperty("expectedRevision", out var revision) || !revision.TryGetInt64(out var expectedRevision) || expectedRevision < 0 ||
                !root.TryGetProperty("mode", out var modeValue) || modeValue.ValueKind != JsonValueKind.String ||
                !Enum.TryParse<ModelProviderMode>(modeValue.GetString(), ignoreCase: false, out var mode) || !Enum.IsDefined(mode) ||
                !root.TryGetProperty("maxAnswerProposalOperationsPerApplication", out var maximum) || !maximum.TryGetInt32(out var max))
                return null;
            return new(expectedRevision, mode, max);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return null; }
    }
}
