# W08 resumable synthetic question review

**Implemented and focused-test date:** 2026-09-20

## Implemented behavior

`DemoWorkflow` now uses the common `ApplicationQuestions` rules to build and resolve the application question snapshot. The default constructor path keeps the existing four deterministic fixture questions. The opt-in extended path adds three reviewed preferences:

- work mode: `remote` or `hybrid`;
- travel: `true` or `false`;
- contact method: `email` or `phone`.

The call-window question is added only after the reviewed contact method resolves to `phone`; it is limited to 80 characters. The synthetic job uses the stable role-group identifier `dotnet-developer`.

An unresolved extended question creates the journal record in `NeedsInput` instead of abandoning draft creation. The record contains the question snapshot and keeps its application GUID across restart and review. `GetStateAsync` exposes the current payload hash and a question/resolution snapshot under the application view for the authenticated local UI.

The UI-only review method is:

```csharp
Task<SyntheticQuestionReview> ReviewAnswersFromUiAsync(
    Guid applicationRef,
    string expectedPayloadHash,
    IReadOnlyList<ReviewedAnswerMemoryUpdate> reviewedAnswers);
```

It binds the review to the current application and payload hash. The common Core rules reject missing, sensitive, attestation, terminal, or invalidly scoped questions. The synthetic adapter additionally rejects values outside its fixed preference vocabulary. Scope identifiers are derived by Core; an application-scoped answer is stored with the true application GUID rather than a caller-supplied scope ID.

Before persisting a valid answer change, the workflow cancels and disposes the prepared browser and clears its UI session binding. It stores the reviewed memory as a new encrypted profile revision, re-resolves the same application, and atomically replaces the journal package while clearing sharing approval, submission approval, host-review request, evidence, and stale error state. The changed profile version, answers, and question snapshot all participate in the application payload hash.

## Recovery property

Initialization loads the latest encrypted profile revision after recovering interrupted browser work. For a nonterminal application, it re-resolves and clears approvals when either the profile version or the active versioned question set differs from the journal package. This closes the crash window in which profile memory could be saved before the journal reset. An unrelated `ManualTakeoverRequired` pause is preserved when neither the profile version nor question definition changed.

No parallel plaintext profile or answer store was added.

## TDD evidence

The focused RED command exited **1** because `DashboardHost` already requested the planned four-argument workflow constructor and `DemoWorkflow` did not yet provide it:

```text
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~ResumableSyntheticTests"

CS1729: DemoWorkflow does not contain a constructor that takes 4 arguments.
```

After implementation, the real browser workflow passed **1/1**:

```text
Passed: 1, Failed: 0, Skipped: 0, Total: 1
artifacts/verification/w08-resume-green/w08-resume-green-2.trx
```

The test proves one continuous scenario:

1. the extended draft persists `NeedsInput` with seven current questions and zero submission POSTs;
2. restart retains the same application GUID and paused state;
3. three application-scoped reviewed answers store the real application GUID and resume `ReadyForDataSharing`;
4. after browser preparation and stored submission approval, editing work mode changes the payload hash, clears sharing/submission approval and host-review state, disposes the old browser, and prevents execution with the old approval;
5. fresh sharing and submission produce exactly one verified synthetic receipt whose preferences match the revised answers.

The original four-question path was then checked against the existing host coordinator and dashboard browser happy path. The compatibility filter passed **7/7**:

```text
Passed: 7, Failed: 0, Skipped: 0, Total: 7
artifacts/verification/w08-resume-green/w08-compat-green.trx
```

## Authenticated synthetic UI

The local dashboard now exposes `POST /api/applications/answers` next to draft creation. The existing loopback Host/IP, same-origin, UI session cookie, and CSRF middleware protects the route. Its typed body contains only the current application GUID, expected payload hash, and reviewed answer-memory updates; it cannot grant sharing or submission approval and is not mapped under `/internal/mcp`.

The synthetic question panel shows missing and resolved preferences with fixed controls for work mode, travel, contact method, and the conditional call window. A user selects Application, Company, RoleGroup, or Default scope and may set an expiry. The save button requires an unchecked-by-default review confirmation. Editing an answer, scope, or expiry clears it; leaving the synthetic view unmounts the panel; and a changed server payload hash remounts it with a fresh unchecked confirmation.

The UI RED run reached persisted `NeedsInput` and then timed out looking for the first work-mode control, because the panel did not exist. After the route and panel were implemented, the focused UI and expiry suite passed **2/2**:

```text
Passed: 2, Failed: 0, Skipped: 0, Total: 2
artifacts/verification/w08-ui-green/w08-ui-expiry-green.trx
```

The browser case proves pause persistence across reload, fixed preference review, confirmation reset after a view change and after an edited value, payload-change confirmation reset, fresh sharing and final approval, and exactly one verified receipt. The second case proves an expired reviewed preference is re-resolved to `NeedsInput` on state refresh with zero POSTs.

The final browser compatibility filter added the conditional phone step and the unchanged default dashboard happy path. It passed **3/3**:

```text
Passed: 3, Failed: 0, Skipped: 0, Total: 3
artifacts/verification/w08-ui-green/w08-ui-final-green.trx
```

In the extended flow, selecting `phone` first exposes a new call-window question and changes the payload hash. The UI requires a fresh confirmation for that second review. The verified receipt contains the exact reviewed call window, while the fixture never invents a real phone field.

The expiration check is also enforced immediately before data-sharing approval, final UI approval, and execution of a previously stored host approval. If the resolved package changed, the workflow disposes the prepared browser, clears both approvals and host-review state, persists the new package, and blocks sending.

## Limits

- This is the fixed synthetic adapter. It does not enable arbitrary employer questions, live-site submission, or model-created approvals.
- Conditional phone review takes two explicit UI reviews when the contact method was previously unknown: first `phone`, then the newly visible call-window question. The workflow never invents a phone number or call window.
- The route remains a trusted local UI operation. No MCP/model approval surface or live-site form adapter was added.
