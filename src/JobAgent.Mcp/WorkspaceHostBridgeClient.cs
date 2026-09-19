using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using JobAgent.Core.Applications;
using JobAgent.Infrastructure.Bridge;
using JobAgent.Infrastructure.Workspace;
using ModelContextProtocol;

namespace JobAgent.Mcp;

// The only network destination comes from the protected, expiring local registration.
// Paths are generated from fixed operations and parsed GUIDs, never from model URLs.
public sealed class WorkspaceHostBridgeClient(RuntimeStore store) : ILocalWorkspaceHost
{
    public Task<HostWorkspaceRefs> GetHostWorkspaceRefsAsync() => SendAsync<HostWorkspaceRefs>("refs");
    public Task<HostProfileSummary> GetHostProfileSummaryAsync(Guid profileRef) =>
        SendAsync<HostProfileSummary>("profiles/" + Ref(profileRef) + "/summary", expected: profileRef);
    public Task<HostPendingProposal> ProposeProfilePatchAsync(HostProfilePatchRequest request) =>
        SendAsync<HostPendingProposal>("profiles/" + Ref(request.ProfileRef) + "/patches", request);
    public Task<HostJobImportResult> ImportJobProposalAsync(HostJobImportRequest request) =>
        SendAsync<HostJobImportResult>("jobs/import", request);
    public Task<HostJobEvaluation> EvaluateJobAsync(Guid jobRef, Guid profileRef) =>
        SendAsync<HostJobEvaluation>("jobs/" + Ref(jobRef) + "/evaluate", new { profileRef }, jobRef, profileRef);
    public Task<HostApplicationState> CreateApplicationAsync(HostCreateApplicationRequest request) =>
        SendAsync<HostApplicationState>("applications", request);
    public Task<HostApplicationQuestions> GetApplicationQuestionsAsync(Guid applicationRef) =>
        SendAsync<HostApplicationQuestions>("applications/" + Ref(applicationRef) + "/questions", expected: applicationRef);
    public Task<HostAnswerProposalResult> ProposeApplicationAnswersAsync(HostAnswerProposalRequest request) =>
        SendAsync<HostAnswerProposalResult>("applications/" + Ref(request.ApplicationRef) + "/answers", request, request.ApplicationRef);
    public Task<HostApplicationState> PrepareApplicationReviewAsync(Guid applicationRef) =>
        SendAsync<HostApplicationState>("applications/" + Ref(applicationRef) + "/review", expected: applicationRef);
    public Task<HostApplicationState> ExecuteApprovedApplicationAsync(Guid applicationRef) =>
        SendAsync<HostApplicationState>("applications/" + Ref(applicationRef) + "/execute", expected: applicationRef, execution: true);
    public Task<HostApplicationState> GetApplicationStatusAsync(Guid applicationRef) =>
        SendAsync<HostApplicationState>("applications/" + Ref(applicationRef) + "/status", expected: applicationRef);
    public Task<HostApplicationState> CancelApplicationAsync(Guid applicationRef) =>
        SendAsync<HostApplicationState>("applications/" + Ref(applicationRef) + "/cancel", expected: applicationRef);

    private async Task<T> SendAsync<T>(string path, object? body = null, Guid? expected = null,
        Guid? expectedProfile = null, bool execution = false)
    {
        if (!store.LocalCommandsEnabled) throw Error("LocalCommandsDisabled");
        var registration = await HostBridgeRegistrationStore.ReadAsync(store.RuntimeDirectory, DateTimeOffset.UtcNow)
            ?? throw Error("UiUnavailable");
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(registration.Origin, "internal/mcp/local/v1/" + path));
        request.Headers.Add("X-JobAgent-Bridge", registration.Token);
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        if (body is not null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(body, LocalToolInputValidator.Json);
            if (bytes.Length > LocalToolInputValidator.MaximumBytes) throw Error("InvalidArguments");
            request.Content = new ByteArrayContent(bytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if ((int)response.StatusCode is >= 300 and < 400) throw Error("BridgeRedirectBlocked");
            using var output = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            int length;
            while ((length = await stream.ReadAsync(buffer)) > 0)
            {
                if (output.Length + length > LocalToolInputValidator.MaximumBytes) throw Error("InvalidBridgeResponse");
                output.Write(buffer, 0, length);
            }
            using var document = JsonDocument.Parse(output.ToArray(), new() { MaxDepth = 8 });
            if (!LocalToolInputValidator.UniqueKeys(document.RootElement)) throw Error("InvalidBridgeResponse");
            if (!response.IsSuccessStatusCode)
            {
                var code = "UiCommandRejected";
                if (response.StatusCode == HttpStatusCode.Conflict && document.RootElement.TryGetProperty("error", out var value) &&
                    value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 80 } error &&
                    error.All(char.IsAsciiLetter)) code = error;
                if (execution && code is not ("BlockedPermission" or "ApplicationNotFound" or "ConsentRequired"))
                    code = "CommandOutcomeUnknownCheckStatus";
                throw Error(code);
            }
            var result = document.RootElement.Deserialize<T>(LocalToolInputValidator.Json);
            if (!ValidResult(result, expected, expectedProfile)) throw Error("InvalidBridgeResponse");
            return result!;
        }
        catch (JsonException) { throw Error("InvalidBridgeResponse"); }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException)
        { throw Error(execution ? "CommandOutcomeUnknownCheckStatus" : "UiUnavailable"); }
    }

    private static bool ValidResult<T>(T? result, Guid? expected, Guid? expectedProfile) => result switch
    {
        HostWorkspaceRefs r => r.Revision >= 0 && r.ProfileRef != Guid.Empty && r.ResumeRef != Guid.Empty && r.JobRef != Guid.Empty &&
            (r.ProfileRef is null ? r.ProfileVersion is null : r.ProfileVersion > 0),
        HostProfileSummary r => Matches(r.ProfileRef, expected) && r.Version > 0 &&
            r.VerifiedSkills is { Count: <= 1500 } && r.VerifiedSkills.All(v => LocalToolInputValidator.Text(v, 80)) &&
            r.Evidence is { Count: <= 1500 } && r.Evidence.All(e => e is not null &&
                LocalToolInputValidator.Text(e.EvidenceRef, 200) && LocalToolInputValidator.Text(e.Skill, 80)),
        HostPendingProposal r => r.ProposalRef != Guid.Empty && r.Revision > 0 && r.BaseVersion > 0 && r.Status == "Pending" && r.RequiresUserReview,
        HostJobImportResult r => r.JobRef != Guid.Empty && r.Revision > 0 && r.RequiresUserReview &&
            r.SuggestedRequirements is { Count: <= 100 } && r.SuggestedRequirements.All(q => q is not null &&
                LocalToolInputValidator.Text(q.RequirementText, 2000) && Enum.IsDefined(q.Type) && Enum.IsDefined(q.Importance)),
        HostJobEvaluation r => Matches(r.JobRef, expected) && Matches(r.ProfileRef, expectedProfile) && r.ProfileVersion > 0 &&
            r.Revision >= 0 && r.Evaluation is not null && Enum.IsDefined(r.Evaluation.Status) &&
            r.Evaluation.Requirements is { Count: <= 100 } && r.Evaluation.Requirements.All(q => q is not null &&
                Enum.IsDefined(q.Assessment) && Enum.IsDefined(q.RequirementType) && q.Reason?.Length is >= 0 and <= 4000),
        HostApplicationState r => Matches(r.ApplicationRef, expected) && r.Revision > 0 && Enum.IsDefined(r.Status) &&
            r.UnresolvedQuestions is >= 0 and <= 100 && !r.SubmissionApproved && r.Evidence is null &&
            r.Status is not (ApplicationStatus.Submitting or ApplicationStatus.SubmittedVerified or ApplicationStatus.SubmittedUnverified) &&
            (r.ReviewRequested ? r.ReviewRef == "local-application:" + r.ApplicationRef.ToString("D") : r.ReviewRef is null),
        HostApplicationQuestions r => Matches(r.ApplicationRef, expected) && r.Revision > 0 && r.Questions is { Count: <= 100 } &&
            r.Questions.All(q => q is not null && LocalToolInputValidator.Text(q.Key, 200) &&
                LocalToolInputValidator.Text(q.Label, 2000) && LocalToolInputValidator.Text(q.Language, 32) &&
                q.MaxLength is null or >= 1 and <= 4000 && Enum.IsDefined(q.Status) && LocalToolInputValidator.References(q.EvidenceRefs)),
        HostAnswerProposalResult r => Matches(r.ApplicationRef, expected) && r.Revision > 0 && r.RequiresUserReview &&
            r.Results is { Count: > 0 and <= 20 } && r.Results.All(a => a is not null &&
                LocalToolInputValidator.Text(a.SemanticKey, 200) && Enum.IsDefined(a.Disposition) && a.Reason?.Length is >= 0 and <= 4000),
        _ => false
    };
    private static bool Matches(Guid actual, Guid? expected) => actual != Guid.Empty && (expected is null || expected == actual);
    private static string Ref(Guid value) => value != Guid.Empty ? value.ToString("D") : throw Error("InvalidReference");
    private static McpProtocolException Error(string code) => new(code, McpErrorCode.InvalidRequest);
}
