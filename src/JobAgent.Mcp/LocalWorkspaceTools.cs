using System.ComponentModel;
using JobAgent.Infrastructure.Workspace;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace JobAgent.Mcp;

public sealed record LocalRuntimeCapabilities(string Mode, bool PaidApiEnabled, bool CanMintApproval,
    string LinkedIn, string PersonalSubmission, string HostExecution, bool CompanionAvailable,
    HostWorkspaceRefs? Workspace, IReadOnlyList<string> Tools)
{
    public int MaxAnswerProposalOperationsPerApplication { get; init; } = 4;
}

[McpServerToolType]
public sealed class LocalWorkspaceTools(WorkspaceHostBridgeClient bridge)
{
    public static readonly string[] ToolNames =
    ["runtime_get_capabilities", "profile_get_summary", "profile_propose_patch", "job_import_text", "job_evaluate",
        "application_create_draft", "application_get_questions", "application_propose_answers",
        "application_prepare_review", "application_execute_approved", "application_get_status", "application_cancel"];

    [McpServerTool(Name = "runtime_get_capabilities", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Reports the explicitly enabled local workspace tools and current opaque references. The companion UI imports and confirms data. No tool can grant consent; personal submission is blocked without an authorized adapter. No paid API is used.")]
    public async Task<LocalRuntimeCapabilities> RuntimeGetCapabilities()
    {
        HostWorkspaceRefs? refs = null;
        try { refs = await bridge.GetHostWorkspaceRefsAsync(); }
        catch (McpProtocolException e) when (e.Message == "UiUnavailable") { }
        return new(refs?.ProviderMode.ToString() ?? "HostMediated", false, false, "Blocked", "BlockedPermission",
            "NotVerifiedOnHost", refs is not null, refs, ToolNames)
        {
            MaxAnswerProposalOperationsPerApplication = refs?.MaxAnswerProposalOperationsPerApplication ?? 4
        };
    }

    [McpServerTool(Name = "profile_get_summary", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Reads only the current confirmed professional skill summary and safe evidence references. Returns no CV text, contact details, or salary.")]
    public Task<HostProfileSummary> ProfileGetSummary(string profileRef) => bridge.GetHostProfileSummaryAsync(Ref(profileRef));

    [McpServerTool(Name = "profile_propose_patch", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Stores a version-bound profile change proposal for local user review. Allowed fields: FullName, Email, Locale, ProfessionalSkill. Never applies, verifies, or approves data. Professional skill proposals require supplied verified evidence references.")]
    public Task<HostPendingProposal> ProfileProposePatch(string profileRef, int baseVersion, HostProfileChange[] changes)
    {
        var request = new HostProfilePatchRequest(Ref(profileRef), baseVersion, changes);
        Require(LocalToolInputValidator.Valid(request));
        return bridge.ProposeProfilePatchAsync(request);
    }

    [McpServerTool(Name = "job_import_text", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Stores provided job text as an unreviewed proposal. Optional employer/title are unverified metadata. The optional HTTP(S) sourceUrl is never fetched or used as a browser destination.")]
    public Task<HostJobImportResult> JobImportText(string text, string? sourceUrl = null, string? employer = null, string? title = null)
    {
        var request = new HostJobImportRequest(text, sourceUrl, employer, title);
        Require(LocalToolInputValidator.Valid(request));
        return bridge.ImportJobProposalAsync(request);
    }

    [McpServerTool(Name = "job_evaluate", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Evaluates the locally reviewed posting against verified professional evidence. Returns typed match/mismatch/unknown assessments, never a private salary floor.")]
    public Task<HostJobEvaluation> JobEvaluate(string jobRef, string profileRef) =>
        bridge.EvaluateJobAsync(Ref(jobRef), Ref(profileRef));

    [McpServerTool(Name = "application_create_draft", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Creates a protected draft bound to the current reviewed job, profile and imported resume references. Uses locally stored questions. Missing answers persist as NeedsInput. Does not open a browser, share data, or submit.")]
    public Task<HostApplicationState> ApplicationCreateDraft(string jobRef, string profileRef, string resumeRef) =>
        bridge.CreateApplicationAsync(new(Ref(jobRef), Ref(profileRef), Ref(resumeRef)));

    [McpServerTool(Name = "application_get_questions", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Reads persisted unresolved or review-required questions. Returns no resolved answer values. Sensitive questions and candidate attestations require the local UI.")]
    public Task<HostApplicationQuestions> ApplicationGetQuestions(string applicationRef) =>
        bridge.GetApplicationQuestionsAsync(Ref(applicationRef));

    [McpServerTool(Name = "application_propose_answers", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Validates and stores grounded answer proposals at the observed workspace revision. Evidence must be currently verified and supplied. Proposals require local user review; they never update trusted answer memory or approve submission. Salary, sensitive data, and candidate attestations are not model-editable.")]
    public Task<HostAnswerProposalResult> ApplicationProposeAnswers(string applicationRef, long baseRevision,
        HostAnswerProposal[] answers, string[] evidenceRefs)
    {
        var request = new HostAnswerProposalRequest(Ref(applicationRef), baseRevision, answers, evidenceRefs);
        Require(LocalToolInputValidator.Valid(request));
        return bridge.ProposeApplicationAnswersAsync(request);
    }

    [McpServerTool(Name = "application_prepare_review", ReadOnly = false, Destructive = false, OpenWorld = false, UseStructuredContent = true),
     Description("Marks a draft for review in the local companion UI and returns its local review reference. Grants no data-sharing or submission approval.")]
    public Task<HostApplicationState> ApplicationPrepareReview(string applicationRef) =>
        bridge.PrepareApplicationReviewAsync(Ref(applicationRef));

    [McpServerTool(Name = "application_execute_approved", ReadOnly = false, Destructive = true, OpenWorld = false, UseStructuredContent = true),
     Description("Requests execution of an existing application. In the personal workspace this always returns BlockedPermission because no authorized live adapter exists. No model parameter can grant approval or provide a destination. Do not blindly retry an uncertain command.")]
    public Task<HostApplicationState> ApplicationExecuteApproved(string applicationRef) =>
        bridge.ExecuteApprovedApplicationAsync(Ref(applicationRef));

    [McpServerTool(Name = "application_get_status", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Reads the referenced protected application's state, revision and review status. Does not reveal answers, raw CV, salary, approval receipts, or secrets.")]
    public Task<HostApplicationState> ApplicationGetStatus(string applicationRef) =>
        bridge.GetApplicationStatusAsync(Ref(applicationRef));

    [McpServerTool(Name = "application_cancel", ReadOnly = false, Destructive = true, OpenWorld = false, UseStructuredContent = true),
     Description("Cancels the referenced editable local draft atomically and prevents future browser work. Cannot withdraw or relabel a submitted or uncertain submission.")]
    public Task<HostApplicationState> ApplicationCancel(string applicationRef) =>
        bridge.CancelApplicationAsync(Ref(applicationRef));

    private static Guid Ref(string value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty
        ? id : throw new McpProtocolException("Reference must be a nonempty canonical GUID.", McpErrorCode.InvalidParams);
    private static void Require(bool condition)
    { if (!condition) throw new McpProtocolException("Invalid local tool arguments.", McpErrorCode.InvalidParams); }
}
