using System.ComponentModel;
using JobAgent.Core.Profiles;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace JobAgent.Mcp;

public sealed record RuntimeCapabilities(
    string Mode,
    bool SyntheticOnly,
    string LinkedIn,
    bool PaidApiEnabled,
    bool CanMintApproval,
    string HostExecution,
    IReadOnlyList<string> ReadOnlyTools)
{
    public IReadOnlyList<string> SyntheticCommands { get; init; } = [];
}

public sealed record ProfileSummary(
    string ProfileRef,
    int Version,
    IReadOnlyList<string> VerifiedSkills);

public sealed record ApplicationEvidenceSummary(
    string ReceiptId,
    string ApplicationKey,
    DateTimeOffset VerifiedAt);

public sealed record ApplicationStatusSummary(
    string ApplicationRef,
    string State,
    ApplicationEvidenceSummary? Evidence,
    string Reference);

[McpServerToolType]
public sealed class ReadOnlyTools(RuntimeStore runtimeStore)
{
    [McpServerTool(Name = "runtime_get_capabilities", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns the constrained local fixture runtime capabilities. Does not call a model, browser, or external service. Approval and synthetic submission require the companion application's local review flow; this read-only inspector cannot approve or submit.")]
    public RuntimeCapabilities RuntimeGetCapabilities() => new(
        "Fixture", true, "Blocked", false, false, "NotVerifiedOnHost",
        ["runtime_get_capabilities", "profile_get_summary", "application_get_status"])
    {
        SyntheticCommands = runtimeStore.SyntheticCommandsEnabled
            ? ["application_create_draft", "application_prepare_review", "application_execute_approved"] : []
    };

    [McpServerTool(Name = "profile_get_summary", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Reads a confirmed local profile by its fixed GUID reference and returns only version and verified skill names.")]
    public async Task<ProfileSummary> ProfileGetSummary(
        [Description("Profile GUID in canonical D format. Paths are not accepted.")] string profileRef,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(profileRef, "D", out var profileId))
            throw new McpProtocolException("profileRef must be a canonical GUID.", McpErrorCode.InvalidParams);
        var profile = await runtimeStore.GetProfileAsync(profileId, cancellationToken);
        if (profile is null)
            throw new McpProtocolException("Profile was not found in the configured runtime.", McpErrorCode.InvalidParams);
        if (profile.VerifiedAt is null)
            throw new McpProtocolException("Profile has not been confirmed.", McpErrorCode.InvalidParams);
        var verifiedSkills = ProfilePolicy.UsableVerifiedFacts(profile, DateTimeOffset.UtcNow)
            .Where(fact => fact.Kind.Equals("professional-skill", StringComparison.OrdinalIgnoreCase))
            .Select(fact => fact.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(profileRef, profile.Version, verifiedSkills);
    }

    [McpServerTool(Name = "application_get_status", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Reads an existing local application journal record by its fixed GUID reference and returns status evidence only.")]
    public async Task<ApplicationStatusSummary> ApplicationGetStatus(
        [Description("Application GUID in canonical D format. Paths are not accepted.")] string applicationRef)
    {
        if (!Guid.TryParseExact(applicationRef, "D", out var applicationId))
            throw new McpProtocolException("applicationRef must be a canonical GUID.", McpErrorCode.InvalidParams);
        var application = await runtimeStore.GetApplicationAsync(applicationId);
        if (application is null)
            throw new McpProtocolException("Application was not found in the configured runtime.", McpErrorCode.InvalidParams);
        var evidence = application.Evidence is null ? null : new ApplicationEvidenceSummary(
            application.Evidence.ReceiptId,
            application.Evidence.ApplicationKey,
            application.Evidence.VerifiedAt);
        return new(applicationRef, application.Draft.Status.ToString(), evidence, application.Draft.JobKey);
    }
}
