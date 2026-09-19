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

public sealed class ManagedBrowserSession(Uri allowedOrigin, TimeProvider? timeProvider = null) : IBrowserSession
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private const string ManualTakeoverSelector = "iframe[title*='captcha' i], iframe[src*='captcha' i], [data-sitekey], input[autocomplete='one-time-code'], input[name*='otp' i], input[name*='mfa' i], input[name*='verificationcode' i], input[name*='verification-code' i]";
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
    private ApprovalReceipt? sharingApproval;
    private ApprovalReceipt? activeSubmissionApproval;
    private CancellationToken submissionCancellation;
    private PolicyException? blockedApproval;
    private TimeSpan activeTime;
    private static readonly HashSet<string> BaseAnswerKeys =
    [
        "contact.name", "contact.email", "salary.expected.monthly.net.TRY",
        "experience.professional.csharp.years"
    ];
    private static readonly HashSet<string> PreferenceAnswerKeys =
    [
        "preference.work.mode", "preference.travel", "preference.contact.method",
        "preference.contact.window"
    ];

    public async Task PrepareAsync(ApplicationDraft draft, ApprovalReceipt? sharing, ResumeDocument resume,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ValidateTarget(draft);
        ApprovalPolicy.Validate(draft, sharing, ApprovalPurpose.ShareData, clock.GetUtcNow());
        sharingApproval = sharing;
        if (draft.ResumeHash != resume.Hash || draft.ResumeRef != resume.Reference)
            throw new PolicyException("ResumeChanged");
        if (preparedHash is not null || page is not null) throw new PolicyException("BrowserAlreadyPrepared");
        if (draft.Answers.GetValueOrDefault("contact.name") != "Synthetic Candidate"
            || draft.Answers.GetValueOrDefault("contact.email") != "candidate@example.invalid")
            throw new PolicyException("SyntheticDataRequired");
        ValidateDraftAnswers(draft);
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
                // Validate after parsing the exact body, at the final outbound boundary.
                ct.ThrowIfCancellationRequested();
                if (method == "POST")
                {
                    submissionCancellation.ThrowIfCancellationRequested();
                    ApprovalPolicy.Validate(draft, activeSubmissionApproval, ApprovalPurpose.Submit, clock.GetUtcNow());
                }
                else ApprovalPolicy.Validate(draft, sharing, ApprovalPurpose.ShareData, clock.GetUtcNow());
                var response = await route.FetchAsync(new() { MaxRedirects = 0, MaxRetries = 0, Timeout = 10000 });
                try
                {
                    if (response.Status is >= 300 and < 400)
                    { blockedRequest = true; await route.AbortAsync(); return; }
                    await route.FulfillAsync(new() { Response = response });
                }
                finally { await response.DisposeAsync(); }
            }
            catch (PolicyException e)
            { blockedApproval = e; await route.AbortAsync(); }
            catch (OperationCanceledException)
            { await route.AbortAsync(); }
            catch (PlaywrightException)
            { await route.AbortAsync(); }
        });
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(10000);
        try
        {
            await ExecuteAsync(new BrowserAction.Navigate(), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.ReadVisibleControls(BrowserStep.Contact), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.FillText(BrowserField.ContactName), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.FillText(BrowserField.ContactEmail), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.Advance(BrowserStep.Contact), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.ReadVisibleControls(BrowserStep.Questions), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.FillText(BrowserField.Salary), draft, resume, ct);
            await ExecuteAsync(new BrowserAction.FillText(BrowserField.ProfessionalYears), draft, resume, ct);
            if (HasPreferences(draft))
            {
                await ExecuteAsync(new BrowserAction.SelectOption(BrowserField.WorkMode), draft, resume, ct);
                await ExecuteAsync(new BrowserAction.SetCheckbox(BrowserField.Travel), draft, resume, ct);
                await ExecuteAsync(new BrowserAction.ChooseRadio(BrowserField.ContactMethod), draft, resume, ct);
                await ExecuteAsync(new BrowserAction.Advance(BrowserStep.Questions), draft, resume, ct);
                await ExecuteAsync(new BrowserAction.ReadVisibleControls(BrowserStep.Final), draft, resume, ct);
                if (draft.Answers["preference.contact.method"] == "phone")
                    await ExecuteAsync(new BrowserAction.FillText(BrowserField.ContactWindow), draft, resume, ct);
            }
            await ExecuteAsync(new BrowserAction.UploadApprovedResume(), draft, resume, ct);
            preparedHash = draft.PayloadHash();
        }
        catch (PlaywrightException) when (blockedApproval is not null) { throw blockedApproval; }
        catch (PlaywrightException) when (blockedRequest) { throw new PolicyException("RecipientChanged"); }
        catch (KeyNotFoundException) when (blockedWebSocket)
        { throw new PolicyException("RecipientChanged"); }
    }

    public async Task<SubmissionEvidence?> SubmitAsync(ApplicationDraft draft, ApprovalReceipt? approval,
        CancellationToken ct = default)
    {
        Guard(ct);
        ValidateTarget(draft);
        ApprovalPolicy.Validate(draft, approval, ApprovalPurpose.Submit, clock.GetUtcNow());
        if (submitted) throw new PolicyException("SubmissionAlreadyAttempted");
        await EnsureReadyForSubmissionAsync(draft, ct);
        var preparedPage = page ?? throw new PolicyException("PackageChanged");
        var fields = ExpectedTextFields(draft);
        Guard(ct);
        await EnsureNoManualTakeoverAsync(ct);
        // Persisted submission claim is made by the workflow before this method. Never retry this click.
        submitted = true;
        activeSubmissionApproval = approval;
        submissionCancellation = ct;
        Interlocked.Exchange(ref submissionRequestAllowance, 1);
        try
        {
            IResponse? response = null;
            await Act(async () => response = await preparedPage.RunAndWaitForResponseAsync(
                () => preparedPage.GetByRole(AriaRole.Button, new() { Name = "Submit synthetic application", Exact = true }).ClickAsync(),
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
            if (HasPreferences(draft) &&
                (ReceiptString(receipt, "workMode") != draft.Answers["preference.work.mode"]
                 || ReceiptBoolean(receipt, "travel") != (draft.Answers["preference.travel"] == "true")
                 || ReceiptString(receipt, "contactMethod") != draft.Answers["preference.contact.method"]
                 || ReceiptString(receipt, "contactWindow") != draft.Answers.GetValueOrDefault("preference.contact.window")))
                return null;
            return new(id, key, hash, clock.GetUtcNow(), draft.PayloadHash());
        }
        catch (Exception e) when (e is PlaywrightException or System.TimeoutException or JsonException or OperationCanceledException)
        { return null; }
        finally
        {
            Interlocked.Exchange(ref submissionRequestAllowance, 0);
            activeSubmissionApproval = null;
            submissionCancellation = default;
        }
    }

    public async Task EnsureReadyForSubmissionAsync(ApplicationDraft draft, CancellationToken ct = default)
    {
        Guard(ct);
        ValidateTarget(draft);
        if (page is null || preparedHash != draft.PayloadHash()) throw new PolicyException("PackageChanged");
        await EnsureNoManualTakeoverAsync(ct);
        await ValidateFormContractAsync(draft, ct);
        var fields = ExpectedTextFields(draft);
        foreach (var field in fields.Where(field => field.Key is
                     "name" or "email" or "salary" or "years" or "applicationKey" or "contactWindow"))
        {
            Guard(ct);
            await EnsureNoManualTakeoverAsync(ct);
            if (await page.Locator($"input[name='{field.Key}']").InputValueAsync() != field.Value)
                throw new PolicyException("FormChanged");
        }
        if (HasPreferences(draft))
        {
            var checkedRadio = page.Locator("input[name='contactMethod']:checked");
            if (await page.Locator("select[name='workMode']").InputValueAsync() != draft.Answers["preference.work.mode"]
                || await page.Locator("input[name='travel']").IsCheckedAsync() != (draft.Answers["preference.travel"] == "true")
                || await checkedRadio.CountAsync() != 1
                || await checkedRadio.InputValueAsync() != draft.Answers["preference.contact.method"])
                throw new PolicyException("FormChanged");
        }
        // Keep this check adjacent to the caller's durable submission claim.
        await EnsureNoManualTakeoverAsync(ct);
    }

    private static string? ReceiptString(JsonElement receipt, string key) =>
        receipt.ValueKind == JsonValueKind.Object && receipt.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? ReceiptBoolean(JsonElement receipt, string key) =>
        receipt.ValueKind == JsonValueKind.Object && receipt.TryGetProperty(key, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static async Task<bool> MatchesApprovedBodyAsync(IRequest request, ApplicationDraft draft,
        ResumeDocument resume, CancellationToken ct)
    {
        var bytes = request.PostDataBuffer;
        if (bytes is null || bytes.Length > 2_100_000 ||
            !MediaTypeHeaderValue.TryParse(request.Headers.GetValueOrDefault("content-type"), out var type)
            || type.MediaType != "multipart/form-data") return false;
        var boundary = HeaderUtilities.RemoveQuotes(type.Boundary).Value;
        if (string.IsNullOrEmpty(boundary) || boundary.Length > 128) return false;
        var expected = ExpectedTextFields(draft);
        expected["synthetic"] = "true";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using var input = new MemoryStream(bytes, false);
        var reader = new MultipartReader(boundary, input) { BodyLengthLimit = 2_000_000, HeadersCountLimit = 16 };
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

    private static void ValidateDraftAnswers(ApplicationDraft draft)
    {
        if (draft.Answers.Keys.Any(key => !BaseAnswerKeys.Contains(key) && !PreferenceAnswerKeys.Contains(key)))
            throw new PolicyException("UnsupportedAnswer");
        if (BaseAnswerKeys.Any(key => !draft.Answers.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)))
            throw new PolicyException("InvalidAnswer");
        if (!draft.Answers.Keys.Any(PreferenceAnswerKeys.Contains)) return;
        if (!draft.Answers.TryGetValue("preference.work.mode", out var workMode)
            || workMode is not ("remote" or "hybrid")
            || !draft.Answers.TryGetValue("preference.travel", out var travel)
            || travel is not ("true" or "false")
            || !draft.Answers.TryGetValue("preference.contact.method", out var contactMethod)
            || contactMethod is not ("email" or "phone"))
            throw new PolicyException("InvalidAnswer");
        var hasWindow = draft.Answers.TryGetValue("preference.contact.window", out var window);
        if (contactMethod == "phone")
        {
            if (!hasWindow || string.IsNullOrWhiteSpace(window)) throw new PolicyException("NeedsInput");
            if (window.Length > 80) throw new PolicyException("InvalidAnswer");
        }
        else if (hasWindow) throw new PolicyException("InvalidAnswer");
    }

    private static bool HasPreferences(ApplicationDraft draft) =>
        draft.Answers.ContainsKey("preference.work.mode");

    private static Dictionary<string, string> ExpectedTextFields(ApplicationDraft draft)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["applicationKey"] = draft.Id.ToString(),
            ["name"] = draft.Answers["contact.name"],
            ["email"] = draft.Answers["contact.email"],
            ["salary"] = draft.Answers["salary.expected.monthly.net.TRY"],
            ["years"] = draft.Answers["experience.professional.csharp.years"]
        };
        if (!HasPreferences(draft)) return expected;
        expected["workMode"] = draft.Answers["preference.work.mode"];
        expected["contactMethod"] = draft.Answers["preference.contact.method"];
        if (draft.Answers["preference.travel"] == "true") expected["travel"] = "true";
        if (draft.Answers["preference.contact.method"] == "phone")
            expected["contactWindow"] = draft.Answers["preference.contact.window"];
        return expected;
    }

    private async Task ExecuteAsync(BrowserAction action, ApplicationDraft draft, ResumeDocument resume,
        CancellationToken ct)
    {
        ApprovalPolicy.Validate(draft, sharingApproval, ApprovalPurpose.ShareData, clock.GetUtcNow());
        switch (action)
        {
            case BrowserAction.Navigate:
                await Act(() => page!.GotoAsync(draft.RecipientOrigin + draft.TargetPath + "?applicationKey=" + draft.Id), ct);
                break;
            case BrowserAction.ReadVisibleControls read:
                await Act(() => ValidateVisibleControlsAsync(draft, read.Step, ct), ct);
                break;
            case BrowserAction.FillText fill:
                var (label, key) = fill.Field switch
                {
                    BrowserField.ContactName => ("Full name", "contact.name"),
                    BrowserField.ContactEmail => ("Email", "contact.email"),
                    BrowserField.Salary => ("Expected monthly net salary (TRY)", "salary.expected.monthly.net.TRY"),
                    BrowserField.ProfessionalYears => ("Professional C# years", "experience.professional.csharp.years"),
                    BrowserField.ContactWindow => ("Preferred call window", "preference.contact.window"),
                    _ => throw new PolicyException("InvalidBrowserAction")
                };
                var textControl = fill.Field == BrowserField.ContactEmail
                    ? page!.GetByRole(AriaRole.Textbox, new() { Name = label, Exact = true })
                    : page!.GetByLabel(label, new() { Exact = true });
                await Act(() => textControl.FillAsync(draft.Answers[key]), ct);
                break;
            case BrowserAction.SelectOption { Field: BrowserField.WorkMode }:
                await Act(() => page!.GetByRole(AriaRole.Combobox, new() { Name = "Work arrangement", Exact = true })
                    .SelectOptionAsync(draft.Answers["preference.work.mode"]), ct);
                break;
            case BrowserAction.SetCheckbox { Field: BrowserField.Travel }:
                await Act(() => page!.GetByRole(AriaRole.Checkbox, new() { Name = "Open to occasional travel", Exact = true })
                    .SetCheckedAsync(draft.Answers["preference.travel"] == "true"), ct);
                break;
            case BrowserAction.ChooseRadio { Field: BrowserField.ContactMethod }:
                var radioName = draft.Answers["preference.contact.method"] == "email" ? "Email" : "Phone";
                await Act(() => page!.GetByRole(AriaRole.Radio, new() { Name = radioName, Exact = true }).CheckAsync(), ct);
                break;
            case BrowserAction.UploadApprovedResume:
                await Act(() => page!.GetByLabel("Resume", new() { Exact = true }).SetInputFilesAsync(new FilePayload
                { Name = resume.FileName, MimeType = "text/plain", Buffer = resume.Bytes.ToArray() }), ct);
                break;
            case BrowserAction.Advance { From: BrowserStep.Contact }:
                await Act(() => page!.GetByRole(AriaRole.Button, new() { Name = "Continue", Exact = true }).ClickAsync(), ct);
                break;
            case BrowserAction.Advance { From: BrowserStep.Questions } when HasPreferences(draft):
                await Act(() => page!.GetByRole(AriaRole.Button, new() { Name = "Continue to final step", Exact = true }).ClickAsync(), ct);
                break;
            default:
                throw new PolicyException("InvalidBrowserAction");
        }
        ApprovalPolicy.Validate(draft, sharingApproval, ApprovalPurpose.ShareData, clock.GetUtcNow());
    }

    private async Task ValidateVisibleControlsAsync(ApplicationDraft draft, BrowserStep step, CancellationToken ct)
    {
        await ValidateFormContractAsync(draft, ct);
        var expectedVisible = step switch
        {
            BrowserStep.Contact => new HashSet<string>(["name", "email"], StringComparer.Ordinal),
            BrowserStep.Questions when HasPreferences(draft) =>
                new HashSet<string>(["salary", "years", "workMode", "travel", "contactMethod"], StringComparer.Ordinal),
            BrowserStep.Questions => new HashSet<string>(["salary", "years", "resume"], StringComparer.Ordinal),
            BrowserStep.Final when draft.Answers["preference.contact.method"] == "phone" =>
                new HashSet<string>(["contactWindow", "resume"], StringComparer.Ordinal),
            BrowserStep.Final => new HashSet<string>(["resume"], StringComparer.Ordinal),
            _ => throw new PolicyException("InvalidBrowserAction")
        };
        foreach (var name in KnownControlCounts(draft).Keys.Where(name => name is not ("synthetic" or "applicationKey")))
        {
            var controls = page!.Locator($"#application [name='{name}']");
            var visible = false;
            for (var index = 0; index < await controls.CountAsync(); index++)
                visible |= await controls.Nth(index).IsVisibleAsync();
            if (visible != expectedVisible.Contains(name)) throw new PolicyException("FormChanged");
        }
    }

    private async Task ValidateFormContractAsync(ApplicationDraft draft, CancellationToken ct)
    {
        Guard(ct);
        await EnsureNoManualTakeoverAsync(ct);
        if (page is null || await page.GetByRole(AriaRole.Heading,
                new() { Name = "SYNTHETIC TEST SITE", Exact = true }).CountAsync() != 1)
            throw new PolicyException("FormChanged");
        var form = page.Locator("#application");
        if (await form.CountAsync() != 1)
            throw new PolicyException("FormChanged");
        var disabledControls = form.Locator("input:disabled,select:disabled,textarea:disabled");
        for (var index = 0; index < await disabledControls.CountAsync(); index++)
            if (!HasPreferences(draft)
                || await disabledControls.Nth(index).GetAttributeAsync("name") != "contactWindow"
                || await disabledControls.Nth(index).IsVisibleAsync())
                throw new PolicyException("FormChanged");

        var expectedCounts = KnownControlCounts(draft);
        var observed = expectedCounts.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        var controls = form.Locator("input[name],select[name],textarea[name]");
        for (var index = 0; index < await controls.CountAsync(); index++)
        {
            ct.ThrowIfCancellationRequested();
            var control = controls.Nth(index);
            var name = await control.GetAttributeAsync("name");
            if (name is null) throw new PolicyException("FormChanged");
            if (!observed.ContainsKey(name))
            {
                var type = await control.GetAttributeAsync("type") ?? "text";
                var required = await control.GetAttributeAsync("required") is not null;
                if (type == "hidden") throw new PolicyException("FormChanged");
                if (required && await control.IsVisibleAsync()) throw new PolicyException("NeedsInput");
                if (required) continue;
                throw new PolicyException("FormChanged");
            }
            observed[name]++;
        }
        if (observed.Any(pair => pair.Value != expectedCounts[pair.Key])) throw new PolicyException("FormChanged");

        await RequireInputAsync("synthetic", "hidden", required: false);
        await RequireInputAsync("applicationKey", "hidden", required: false);
        if (await form.Locator("input[name='synthetic']").InputValueAsync() != "true"
            || await form.Locator("input[name='applicationKey']").InputValueAsync() != draft.Id.ToString())
            throw new PolicyException("FormChanged");
        await RequireInputAsync("name", "text", required: true);
        await RequireInputAsync("email", "email", required: true);
        await RequireInputAsync("salary", "number", required: true);
        await RequireInputAsync("years", "number", required: true);
        await RequireInputAsync("resume", "file", required: true);

        if (HasPreferences(draft))
        {
            var options = form.Locator("select[name='workMode'] option");
            var optionValues = new List<string?>();
            for (var index = 0; index < await options.CountAsync(); index++)
                optionValues.Add(await options.Nth(index).GetAttributeAsync("value"));
            if (!optionValues.SequenceEqual(new string?[] { "", "remote", "hybrid" }))
                throw new PolicyException("FormChanged");
            if (await form.Locator("select[name='workMode'][required]").CountAsync() != 1)
                throw new PolicyException("FormChanged");
            await RequireInputAsync("travel", "checkbox", required: false);
            if (await form.Locator("input[name='travel']").GetAttributeAsync("value") != "true")
                throw new PolicyException("FormChanged");
            var radios = form.Locator("input[name='contactMethod']");
            var radioValues = new List<string?>();
            for (var index = 0; index < await radios.CountAsync(); index++)
            {
                if ((await radios.Nth(index).GetAttributeAsync("type") ?? "text") != "radio"
                    || await radios.Nth(index).GetAttributeAsync("required") is null)
                    throw new PolicyException("FormChanged");
                radioValues.Add(await radios.Nth(index).GetAttributeAsync("value"));
            }
            if (!radioValues.SequenceEqual(new string?[] { "email", "phone" }))
                throw new PolicyException("FormChanged");
            var contactWindow = form.Locator("input[name='contactWindow']");
            if (await contactWindow.CountAsync() != 1
                || (await contactWindow.GetAttributeAsync("type") ?? "text") != "text"
                || await contactWindow.GetAttributeAsync("maxlength") != "80")
                throw new PolicyException("FormChanged");
            var contactWindowVisible = await contactWindow.IsVisibleAsync();
            var contactWindowRequired = await contactWindow.GetAttributeAsync("required") is not null;
            var contactWindowDisabled = await contactWindow.IsDisabledAsync();
            if (contactWindowVisible
                ? draft.Answers["preference.contact.method"] != "phone" || !contactWindowRequired || contactWindowDisabled
                : contactWindowRequired || !contactWindowDisabled)
                throw new PolicyException("FormChanged");
        }
        return;

        async Task RequireInputAsync(string name, string type, bool required)
        {
            var input = form.Locator($"input[name='{name}']");
            if (await input.CountAsync() != 1 || (await input.GetAttributeAsync("type") ?? "text") != type
                || (await input.GetAttributeAsync("required") is not null) != required)
                throw new PolicyException("FormChanged");
        }
    }

    private static Dictionary<string, int> KnownControlCounts(ApplicationDraft draft)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["synthetic"] = 1,
            ["applicationKey"] = 1,
            ["name"] = 1,
            ["email"] = 1,
            ["salary"] = 1,
            ["years"] = 1,
            ["resume"] = 1
        };
        if (HasPreferences(draft))
        {
            counts["workMode"] = 1;
            counts["travel"] = 1;
            counts["contactMethod"] = 2;
            counts["contactWindow"] = 1;
        }
        return counts;
    }
    private async Task Act(Func<Task> action, CancellationToken ct)
    {
        Guard(ct);
        await EnsureNoManualTakeoverAsync(ct);
        if (++actionCount > 40 || activeTime > TimeSpan.FromMinutes(15)) throw new PolicyException("BudgetExceeded");
        var clock = Stopwatch.StartNew();
        try { await action(); }
        finally { activeTime += clock.Elapsed; }
        Guard(ct);
        await EnsureNoManualTakeoverAsync(ct);
    }
    private async Task EnsureNoManualTakeoverAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (page is null) return;
        var candidates = page.Locator(ManualTakeoverSelector);
        var count = await candidates.CountAsync();
        for (var index = 0; index < count; index++)
            if (await candidates.Nth(index).IsVisibleAsync())
                throw new PolicyException("ManualTakeoverRequired");
    }
    private void Guard(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (blockedApproval is not null) throw blockedApproval;
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
