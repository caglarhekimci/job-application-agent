# W09 Infrastructure coordinator evidence

Date: 2026-09-20

## Implemented boundary

The Infrastructure workflow now provides a narrow coordinator contract for the synthetic fixture:

```csharp
public sealed record HostApplicationSummary(
    Guid ApplicationRef,
    ApplicationStatus Status,
    bool ReviewRequested,
    bool SubmissionApproved,
    string? ReceiptId);

Task<HostApplicationSummary> CreateSyntheticDraftForHostAsync();
Task<HostApplicationSummary> RequestHostReviewAsync(Guid applicationRef);
Task<HostApplicationSummary> ApproveForHostFromUiAsync(Guid applicationRef, string sessionId);
Task<HostApplicationSummary> ExecuteAlreadyApprovedAsync(Guid applicationRef);
Task<HostApplicationSummary> GetHostStatusAsync(Guid applicationRef);
```

The host summary contains no profile, contact, answer, approval-token, or session value. `ReceiptId` is populated only from stored submission evidence. `SubmissionApproved` is true only while the stored submit receipt is unused, unexpired, and still bound to the current application payload, recipient, profile version, resume, and purpose.

Draft creation initializes storage but does not import or confirm a profile. Until the local UI has confirmed the synthetic profile, it fails with `ProfileReviewRequired`. Every coordinator operation requires the supplied GUID to equal the workflow's current synthetic application.

## Approval and execution properties

- A host may request review, read safe status, or ask to execute. It cannot mint or supply an approval receipt.
- `ApproveForHostFromUiAsync` is the trusted UI-only step. It checks the active browser and UI session, performs the pre-submission challenge check, and stores a payload-bound approval without posting.
- `ExecuteAlreadyApprovedAsync` accepts only the application reference. It validates the stored approval, checks again for CAPTCHA/MFA immediately before the durable claim, and atomically consumes the receipt through `ApplicationJournal.ClaimSubmissionAsync`.
- Direct UI submission uses the same stored-approval execution helper. Concurrent calls serialize in the workflow, while the journal revision and atomic claim remain the durable single-attempt guard.
- A CAPTCHA/MFA found before the claim moves the application to `NeedsInput`, clears the unconsumed submit receipt, records `ManualTakeoverRequired`, closes the automated browser, and sends no POST.
- Once a claim exists, any uncertain browser result remains `SubmittedUnverified`; the coordinator does not retry.
- Recovery preserves a consumed sharing receipt and the persisted host-review request. It clears any unconsumed submit receipt when recovering `Filling` or `AwaitingSubmissionApproval` to `ReadyForDataSharing`, so a restarted browser session requires fresh sharing and submission consent.

The UI state projection now includes only `hostReviewRequested` and the dynamically validated `submissionApproved` booleans alongside the existing application view.

## Test evidence

The focused test was first run before the DTO and coordinator API existed and failed compilation with `HostApplicationSummary` missing. After implementation:

```text
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~HostCoordinatorTests" --verbosity minimal

Passed: 6, Failed: 0, Skipped: 0, Total: 6
```

The cases cover profile-confirmation and current-GUID binding, execution denial without approval and without journal mutation, UI approval followed by two concurrent execution requests producing one POST and one receipt, late pre-claim MFA pause, restart invalidation before new sharing, and the existing direct UI path using the common claim/execution logic.

## Deliberate limit

This change is only the Infrastructure coordinator and its synthetic tests. It does not expose HTTP, CLI, or MCP commands and does not establish an actual Codex-host integration. The parent integration must keep approval absent from model-callable surfaces, keep the loopback bridge authenticated and origin-bound, and retain its explicit `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1` opt-in.
