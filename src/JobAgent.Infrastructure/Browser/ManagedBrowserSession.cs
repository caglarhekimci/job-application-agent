using JobAgent.Core.Applications;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace JobAgent.Infrastructure.Browser;

public sealed record ResumeDocument(string Reference, string FileName, byte[] Bytes)
{
    public string Hash => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Bytes));
}

public sealed class ManagedBrowserSession(Uri allowedOrigin) : IAsyncDisposable
{
    public Uri AllowedOrigin { get; } = allowedOrigin;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IPage? page;
    private string? preparedHash;
    private bool submitted;
    private bool blockedRequest;
    private bool blockedWebSocket;
    private int actionCount;
    private int submissionRequestAllowance;
    private TimeSpan activeTime;

    public async Task PrepareAsync(ApplicationDraft draft, ApprovalReceipt? sharing, ResumeDocument resume,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateTarget(draft);
        ApprovalPolicy.Validate(draft, sharing, ApprovalPurpose.ShareData, DateTimeOffset.UtcNow);
        if (draft.ResumeHash != resume.Hash || draft.ResumeRef != resume.Reference)
            throw new PolicyException("ResumeChanged");
        if (preparedHash is not null || page is not null) throw new PolicyException("BrowserAlreadyPrepared");
        if (draft.Answers.GetValueOrDefault("contact.name") != "Synthetic Candidate"
            || draft.Answers.GetValueOrDefault("contact.email") != "candidate@example.invalid")
            throw new PolicyException("SyntheticDataRequired");
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync(new() { AcceptDownloads = false, ServiceWorkers = ServiceWorkerPolicy.Block });
        // A routed socket never reaches a server unless ConnectToServer is explicitly called.
        await context.RouteWebSocketAsync("**/*", socket =>
        {
            blockedWebSocket = true;
            blockedRequest = true;
            // Explicit close handler avoids forwarding page-close frames to a nonexistent server.
            socket.OnClose((_, _) => { });
            _ = socket.CloseAsync(new() { Code = 1008, Reason = "BlockedByPolicy" });
        });
        await context.RouteAsync("**/*", async route =>
        {
            if (!IsAllowed(route.Request.Url) || ct.IsCancellationRequested)
            { blockedRequest = true; await route.AbortAsync(); return; }
            var method = route.Request.Method;
            if (method != "GET" && (method != "POST" || new Uri(route.Request.Url).PathAndQuery != "/api/applications"
                || Interlocked.Exchange(ref submissionRequestAllowance, 0) != 1))
            { blockedRequest = true; await route.AbortAsync(); return; }
            if (method == "POST" && !await MatchesApprovedBodyAsync(route.Request, draft, resume, ct))
            { blockedRequest = true; await route.AbortAsync(); return; }
            try
            {
                // Route.Continue lets Chromium follow redirects and retry a lost POST internally.
                // Fetch explicitly disables both; a redirect is rejected before the next hop.
                var response = await route.FetchAsync(new() { MaxRedirects = 0, MaxRetries = 0, Timeout = 10000 });
                try
                {
                    if (response.Status is >= 300 and < 400)
                    { blockedRequest = true; await route.AbortAsync(); return; }
                    await route.FulfillAsync(new() { Response = response });
                }
                finally { await response.DisposeAsync(); }
            }
            catch (PlaywrightException)
            { await route.AbortAsync(); }
        });
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(10000);
        try
        {
            await Act(() => page.GotoAsync(draft.RecipientOrigin + draft.TargetPath + "?applicationKey=" + draft.Id), ct);
            await ValidateFormAsync(ct);
            await Fill("Full name", draft.Answers["contact.name"], ct);
            await Fill("Email", draft.Answers["contact.email"], ct);
            await Act(() => page.GetByRole(AriaRole.Button, new() { Name = "Continue", Exact = true }).ClickAsync(), ct);
            await Fill("Expected monthly net salary (TRY)", draft.Answers["salary.expected.monthly.net.TRY"], ct);
            await Fill("Professional C# years", draft.Answers["experience.professional.csharp.years"], ct);
            await Act(() => page.GetByLabel("Resume", new() { Exact = true }).SetInputFilesAsync(new FilePayload
            { Name = resume.FileName, MimeType = "text/plain", Buffer = resume.Bytes.ToArray() }), ct);
            preparedHash = draft.PayloadHash();
        }
        catch (PlaywrightException) when (blockedRequest) { throw new PolicyException("RecipientChanged"); }
        catch (KeyNotFoundException) when (blockedWebSocket)
        { throw new PolicyException("RecipientChanged"); }
    }

    public async Task<SubmissionEvidence?> SubmitAsync(ApplicationDraft draft, ApprovalReceipt? approval,
        CancellationToken ct = default)
    {
        Guard(ct);
        ValidateTarget(draft);
        ApprovalPolicy.Validate(draft, approval, ApprovalPurpose.Submit, DateTimeOffset.UtcNow);
        if (submitted) throw new PolicyException("SubmissionAlreadyAttempted");
        if (page is null || preparedHash != draft.PayloadHash()) throw new PolicyException("PackageChanged");
        await ValidateFormAsync(ct);
        var fields = new Dictionary<string, string>
        {
            ["name"] = draft.Answers["contact.name"],
            ["email"] = draft.Answers["contact.email"],
            ["salary"] = draft.Answers["salary.expected.monthly.net.TRY"],
            ["years"] = draft.Answers["experience.professional.csharp.years"],
            ["applicationKey"] = draft.Id.ToString()
        };
        foreach (var field in fields)
        {
            Guard(ct);
            if (await page.Locator($"input[name='{field.Key}']").InputValueAsync() != field.Value)
                throw new PolicyException("FormChanged");
        }
        Guard(ct);
        // Persisted submission claim is made by the workflow before this method. Never retry this click.
        submitted = true;
        Interlocked.Exchange(ref submissionRequestAllowance, 1);
        try
        {
            IResponse? response = null;
            await Act(async () => response = await page.RunAndWaitForResponseAsync(
                () => page.GetByRole(AriaRole.Button, new() { Name = "Submit synthetic application", Exact = true }).ClickAsync(),
                r => r.Url == draft.RecipientOrigin + "/api/applications" && r.Request.Method == "POST",
                new() { Timeout = 10000 }), ct);
            if (response is null || !response.Ok) return null;
            var json = await response.JsonAsync();
            if (json is not JsonElement receipt) return null;
            var id = ReceiptString(receipt, "id");
            var key = ReceiptString(receipt, "applicationKey");
            var hash = ReceiptString(receipt, "resumeHash");
            if (id?.StartsWith("SYN-", StringComparison.Ordinal) != true || key != draft.Id.ToString() || hash != draft.ResumeHash)
                return null;
            if (ReceiptString(receipt, "salary") != fields["salary"]
                || ReceiptString(receipt, "professionalYears") != fields["years"]
                || ReceiptString(receipt, "fileName") != "synthetic-resume.txt") return null;
            return new(id, key, hash, DateTimeOffset.UtcNow, draft.PayloadHash());
        }
        catch (Exception e) when (e is PlaywrightException or System.TimeoutException or JsonException or OperationCanceledException)
        { return null; }
        finally { Interlocked.Exchange(ref submissionRequestAllowance, 0); }
    }

    private static string? ReceiptString(JsonElement receipt, string key) =>
        receipt.ValueKind == JsonValueKind.Object && receipt.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static async Task<bool> MatchesApprovedBodyAsync(IRequest request, ApplicationDraft draft,
        ResumeDocument resume, CancellationToken ct)
    {
        var bytes = request.PostDataBuffer;
        if (bytes is null || bytes.Length > 2_100_000 ||
            !MediaTypeHeaderValue.TryParse(request.Headers.GetValueOrDefault("content-type"), out var type)
            || type.MediaType != "multipart/form-data") return false;
        var boundary = HeaderUtilities.RemoveQuotes(type.Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > 128) return false;
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["synthetic"] = "true",
            ["applicationKey"] = draft.Id.ToString(),
            ["name"] = draft.Answers["contact.name"],
            ["email"] = draft.Answers["contact.email"],
            ["salary"] = draft.Answers["salary.expected.monthly.net.TRY"],
            ["years"] = draft.Answers["experience.professional.csharp.years"]
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var input = new MemoryStream(bytes, false);
        var reader = new MultipartReader(boundary, input) { BodyLengthLimit = 2_000_000, HeadersCountLimit = 8 };
        try
        {
            while (await reader.ReadNextSectionAsync(ct) is { } section)
            {
                if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                    || disposition.DispositionType != "form-data") return false;
                var name = HeaderUtilities.RemoveQuotes(disposition.Name).Value;
                if (name is null || !seen.Add(name)) return false;
                using var body = new MemoryStream();
                await section.Body.CopyToAsync(body, ct);
                if (name == "resume")
                {
                    if (HeaderUtilities.RemoveQuotes(disposition.FileName).Value != resume.FileName
                        || Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(body.ToArray())) != draft.ResumeHash)
                        return false;
                }
                else if (disposition.FileName.HasValue || !expected.TryGetValue(name, out var value)
                    || System.Text.Encoding.UTF8.GetString(body.ToArray()) != value) return false;
            }
            return seen.SetEquals(expected.Keys.Append("resume"));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or OperationCanceledException)
        { return false; }
    }

    private async Task ValidateFormAsync(CancellationToken ct)
    {
        Guard(ct);
        if (page is null || await page.Locator("input").CountAsync() != 7
            || await page.Locator("input[name=synthetic]").InputValueAsync() != "true"
            || await page.GetByRole(AriaRole.Heading, new() { Name = "SYNTHETIC TEST SITE", Exact = true }).CountAsync() != 1)
            throw new PolicyException("FormChanged");
    }

    private Task Fill(string label, string value, CancellationToken ct) =>
        Act(() => page!.GetByLabel(label, new() { Exact = true }).FillAsync(value), ct);
    private async Task Act(Func<Task> action, CancellationToken ct)
    {
        Guard(ct);
        if (++actionCount > 40 || activeTime > TimeSpan.FromMinutes(15)) throw new PolicyException("BudgetExceeded");
        var clock = Stopwatch.StartNew();
        try { await action(); }
        finally { activeTime += clock.Elapsed; }
        Guard(ct);
    }
    private void Guard(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (blockedRequest || page is not null && page.Url != "about:blank" && !IsAllowed(page.Url))
            throw new PolicyException("RecipientChanged");
    }
    private void ValidateTarget(ApplicationDraft draft)
    {
        if (AllowedOrigin.Scheme != "http" || AllowedOrigin.Host != "127.0.0.1"
            || AllowedOrigin.AbsolutePath != "/" || !draft.Synthetic
            || draft.RecipientOrigin != AllowedOrigin.GetLeftPart(UriPartial.Authority)
            || draft.TargetPath != "/jobs/synthetic-dotnet")
            throw new PolicyException("PermissionRequired");
    }
    private bool IsAllowed(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == "http" && uri.Host == "127.0.0.1" && uri.Port == AllowedOrigin.Port
        && string.IsNullOrEmpty(uri.UserInfo);
    public async ValueTask DisposeAsync()
    {
        try { if (browser is not null) await browser.DisposeAsync(); }
        // Playwright 1.62's WebSocketRoute assumes optional close-event fields exist.
        // Only suppress that transport cleanup fault after a socket was already denied.
        catch (KeyNotFoundException) when (blockedWebSocket) { }
        finally { playwright?.Dispose(); }
    }
}
