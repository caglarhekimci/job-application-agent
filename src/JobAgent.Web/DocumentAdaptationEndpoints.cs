using JobAgent.Core.Documents;
using JobAgent.Infrastructure.Workspace;

namespace JobAgent.Web;

public static class DocumentAdaptationEndpoints
{
    public static void Map(WebApplication app)
    {
        // DashboardHost's loopback UI cookie, exact Origin and CSRF middleware protects these routes.
        app.MapGet("/api/workspace/applications/{id:guid}/document-adaptation",
            (LocalWorkspace workspace, Guid id) => workspace.GetDocumentAdaptationAsync(id));
        app.MapPost("/api/workspace/applications/{id:guid}/document-adaptation/propose",
            (LocalWorkspace workspace, Guid id, DocumentAdaptationProposalRequest request) =>
                workspace.ProposeDocumentAdaptationFromUiAsync(new(id, request.ExpectedRevision,
                    request.ExpectedApplicationPayloadHash)));
        app.MapPost("/api/workspace/applications/{id:guid}/document-adaptation/approve",
            (LocalWorkspace workspace, Guid id, DocumentAdaptationApprovalRequest request) =>
                workspace.ApproveDocumentAdaptationFromUiAsync(new(id, request.ExpectedRevision,
                    request.ExpectedBundleHash)));
        app.MapGet("/api/workspace/applications/{id:guid}/document-adaptation/export/{kind}/{bundleHash}",
            async (LocalWorkspace workspace, Guid id, string kind, string bundleHash) =>
            {
                if (bundleHash.Length != 64 || !bundleHash.All(Uri.IsHexDigit))
                    return Results.BadRequest(new { error = "InvalidDocumentHash" });
                var documentKind = kind switch
                {
                    "resume" => AdaptedDocumentKind.Resume,
                    "cover-letter" => AdaptedDocumentKind.CoverLetter,
                    _ => (AdaptedDocumentKind?)null
                };
                if (documentKind is null) return Results.BadRequest(new { error = "UnknownDocumentKind" });
                var export = await workspace.ExportApprovedDocumentAsync(id, bundleHash, documentKind.Value);
                return Results.File(export.Bytes, "text/plain; charset=utf-8", export.FileName);
            });
    }
}

public sealed record DocumentAdaptationProposalRequest(long ExpectedRevision,
    string ExpectedApplicationPayloadHash);
public sealed record DocumentAdaptationApprovalRequest(long ExpectedRevision, string ExpectedBundleHash);
