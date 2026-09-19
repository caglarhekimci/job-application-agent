using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Bridge;
using ModelContextProtocol;
using JobAgent.Core.Applications;

namespace JobAgent.Mcp;

public enum HostBridgeOperation { CreateDraft, PrepareReview, ExecuteApproved }

public sealed class LocalHostBridgeClient(RuntimeStore store)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<HostApplicationSummary> SendAsync(HostBridgeOperation operation, Guid? applicationRef,
        CancellationToken cancellationToken)
    {
        var registration = await HostBridgeRegistrationStore.ReadAsync(store.RuntimeDirectory, DateTimeOffset.UtcNow)
            ?? throw Error("UiUnavailable");
        var suffix = operation switch
        {
            HostBridgeOperation.CreateDraft when applicationRef is null => "",
            HostBridgeOperation.PrepareReview when applicationRef is { } id && id != Guid.Empty => "/" + id.ToString("D") + "/review",
            HostBridgeOperation.ExecuteApproved when applicationRef is { } id && id != Guid.Empty => "/" + id.ToString("D") + "/execute",
            _ => throw Error("InvalidCommand")
        };
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(registration.Origin, "internal/mcp/applications" + suffix));
        request.Headers.Add("X-JobAgent-Bridge", registration.Token);
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is >= 300 and < 400) throw Error("BridgeRedirectBlocked");
            using var body = new MemoryStream();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[2048];
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (body.Length + read > 8192) throw Error("InvalidBridgeResponse");
                body.Write(buffer, 0, read);
            }
            if (!response.IsSuccessStatusCode)
            {
                var code = "UiCommandRejected";
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    using var error = JsonDocument.Parse(body.ToArray());
                    if (error.RootElement.TryGetProperty("error", out var value) && value.ValueKind == JsonValueKind.String &&
                        value.GetString() is { Length: > 0 and <= 80 } text && text.All(char.IsAsciiLetter))
                        code = text;
                }
                if (operation == HostBridgeOperation.ExecuteApproved &&
                    code is not ("ConsentRequired" or "ApprovalExpired" or "PackageChanged" or
                        "SubmissionNotReady" or "ApplicationNotFound"))
                    code = "CommandOutcomeUnknownCheckStatus";
                throw Error(code);
            }
            var result = JsonSerializer.Deserialize<HostApplicationSummary>(body.ToArray(), Json);
            if (result is null || result.ApplicationRef == Guid.Empty ||
                (applicationRef is { } expected && result.ApplicationRef != expected) ||
                !Enum.IsDefined(result.Status) || result.ReceiptId?.Length > 200 ||
                (result.Status == ApplicationStatus.SubmittedVerified
                    ? string.IsNullOrWhiteSpace(result.ReceiptId) : result.ReceiptId is not null) ||
                (result.SubmissionApproved && result.Status != ApplicationStatus.AwaitingSubmissionApproval))
                throw Error("InvalidBridgeResponse");
            return result;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or IOException or JsonException)
        {
            // Never retry an execution request after transport uncertainty. The journal status is authoritative.
            throw Error(operation == HostBridgeOperation.ExecuteApproved ? "CommandOutcomeUnknownCheckStatus" : "UiUnavailable");
        }
    }

    private static McpProtocolException Error(string code) => new(code, McpErrorCode.InvalidRequest);
}
