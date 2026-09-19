using System.ComponentModel;
using JobAgent.Infrastructure.Applications;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace JobAgent.Mcp;

[McpServerToolType]
public sealed class SyntheticCommandTools(LocalHostBridgeClient bridge)
{
    [McpServerTool(Name = "application_create_draft", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Creates the fixed synthetic draft in the running local companion after UI profile confirmation. No external employer, URL, file, or approval input is accepted.")]
    public Task<HostApplicationSummary> CreateDraft(CancellationToken cancellationToken) =>
        bridge.SendAsync(HostBridgeOperation.CreateDraft, null, cancellationToken);

    [McpServerTool(Name = "application_prepare_review", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Requests review of the fixed synthetic package in the trusted local companion UI. Does not grant sharing or submission permission. Ask the user to review there.")]
    public Task<HostApplicationSummary> PrepareReview(string applicationRef, CancellationToken cancellationToken) =>
        bridge.SendAsync(HostBridgeOperation.PrepareReview, Parse(applicationRef), cancellationToken);

    [McpServerTool(Name = "application_execute_approved", ReadOnly = false, Destructive = true, OpenWorld = false, UseStructuredContent = true),
     Description("Submits only the fixed local synthetic application after a valid approval was stored by the companion UI. Never creates approval. No real employer is contacted. On transport uncertainty use application_get_status; never blindly retry.")]
    public Task<HostApplicationSummary> ExecuteApproved(string applicationRef, CancellationToken cancellationToken) =>
        bridge.SendAsync(HostBridgeOperation.ExecuteApproved, Parse(applicationRef), cancellationToken);

    private static Guid Parse(string value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty
        ? id : throw new McpProtocolException("applicationRef must be a canonical GUID.", McpErrorCode.InvalidParams);
}
