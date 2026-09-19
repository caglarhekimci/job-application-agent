# Integrated W07/W08 boundary review

**Reviewed:** 2026-09-18
**Scope:** `ManagedBrowserSession`, `DemoWorkflow`, `DashboardHost`, the React dashboard client, and relevant W01/W06/W07/W08 tests. The review treats .NET domain records as trusted in-process contracts, as directed. It assesses actual HTTP/browser boundaries and does not assume hostile reflection, a hostile library caller, or arbitrary same-user process execution.

## Findings

### P1 — A redirect reaches the new origin before the policy stops the workflow

`ManagedBrowserSession` calls `RouteAsync` and checks each routed request URL, then checks the page URL after actions. Playwright routing does not give the current implementation a pre-request veto for the redirected URL: the adversarial redirect test observed one request at the disallowed trap before `RecipientChanged` was raised.

Locations: `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:40`, `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:50`, `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:129`.

Observed reproduction:

```text
AdversarialFormTests.RedirectToNewRecipient_StopsBeforeNewOriginRequest
Expected trap requests: 0
Actual trap requests:   1
```

Impact: the new recipient is contacted before consent/policy validation, and the request can disclose URL metadata such as the application key. A post-navigation URL check detects the violation but cannot undo the request.

Fix target: fetch allowed navigation through an interception path with redirects and retries disabled, inspect every 3xx `Location`, and fulfill only a response whose redirect chain remains authorized. The request must never be continued into Chromium's automatic redirect handling. Keep a regression assertion that the trap receives zero requests.

### P1 — The preparation-stage route permits an application POST before final approval

The route policy currently checks only scheme, host, port, and user-info. It allows every method and path on that origin while `PrepareAsync` is filling the form. An untrusted or changed page can therefore auto-submit `/api/applications` from an input/change handler as soon as the approved data-sharing step fills the fields, before `/api/approve-submit` is called.

Locations: `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:40`, `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:143`.

Reproduction fixture:

1. Serve the expected seven-field form from the allowed origin.
2. Add page script that posts the form to `/api/applications` after the last field or file input changes.
3. Call only `ShareAndFillFromUiAsync`.
4. The current route continues the same-origin POST and the fake site records a receipt before submission approval.

Impact: data-sharing consent becomes de facto submission consent. This breaks the separate final-approval boundary in sections 8, W07, and W08.

Fix target: model route permissions by stage and action, not origin alone. During preparation, reject mutating methods and the known submission endpoint. After a durable submission claim, open a one-shot gate for the exact method, URL, content type, application key, approved field values, and resume hash. Close it after the first request. The gate must be enforced in the request handler, independent of page JavaScript.

### P1 — A single approved click can produce two POSTs after a connection drop

The new network-drop fixture aborts the response after the server stores the receipt. Although `ManagedBrowserSession` calls the click once and sets its local `submitted` flag, the server observed two POSTs. The duplicate occurs below the workflow call boundary, so the local flag, journal claim, and “do not call SubmitAsync twice” rule do not prevent it.

Locations: `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:87`, `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:91`.

Observed reproduction:

```text
AdversarialFormTests.PostSubmitNetworkDrop_IsUnknown_AndNeverRetriesEvenAfterRestart
Expected SubmissionPosts: 1
Actual SubmissionPosts:   2
Stored receipts:          1 (the fake server collapses by application key)
```

Impact: a real recipient that does not honor the application key could create duplicate applications even though application code performs one click. The final state remains correctly uncertain, but “one local call” is not “one network request.”

Fix target: keep the durable idempotency key and require server-side idempotency wherever an adapter/API supports it. For the controlled fixture, perform or intercept submission with transport retries explicitly disabled and add a server assertion on request count. Documentation must retain section 8.4's limitation: exactly-once delivery cannot be guaranteed for arbitrary external sites.

### P1 — Unexpected receipt shapes can strand `Submitting`, then cancellation can mislabel an uncertain attempt

After the click, `SubmitAsync` handles a limited exception set. Missing JSON properties raise `KeyNotFoundException`; wrong JSON value kinds can raise `InvalidOperationException`. These escape both the method's catch filter and `DemoWorkflow`'s filtered catch before the final `Submitting → SubmittedUnverified` update and browser disposal. The journal remains `Submitting`. `CancelAsync` rejects only submitted terminal states, so a subsequent `/api/cancel` can change that durable, potentially accepted attempt to `Cancelled`.

Locations: `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:96`, `src/JobAgent.Infrastructure/Applications/DemoWorkflow.cs:205`, `src/JobAgent.Infrastructure/Applications/DemoWorkflow.cs:209`, `src/JobAgent.Infrastructure/Applications/DemoWorkflow.cs:223`.

Reproduction fixture:

1. Let `/api/applications` accept and store the request.
2. Return HTTP 200 JSON missing `resumeHash`, or return a numeric `id`.
3. Submit through the UI. The request was sent, but the workflow remains `Submitting` because receipt parsing escapes.
4. POST `/api/cancel`. The current code can persist `Cancelled` even though the recipient may have accepted the application.

Impact: an uncertain external result is represented as a user cancellation, contrary to section 8.4. The same process can remain stuck without waiting for restart recovery.

Fix target: once the durable claim exists, use a `try/finally` outcome boundary that maps every non-fatal post-claim failure to `SubmittedUnverified`, preserves the consumed approval/attempt, and disposes the browser. Parse receipts with `TryGetProperty` and kind checks. Forbid `Submitting → Cancelled`; cancellation after claim should stop further browser actions and finish as uncertain unless verified evidence already exists.

### P1 — Normal request routing does not close WebSocket egress

The context installs `RouteAsync("**/*", ...)` for normal requests but no `RouteWebSocketAsync` policy. Playwright 1.62 exposes WebSocket routing as a separate API. A changed allowed page can open `ws://` or `wss://` to another host/port and send the field values outside the approved recipient without setting `blockedRequest`.

Location: `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:40`.

Reproduction fixture:

1. Start a WebSocket trap on another loopback port.
2. Add `new WebSocket(trapUrl)` to the allowed form and send a marker or field value on open.
3. Run `PrepareAsync` with valid sharing approval.
4. The current HTTP route policy has no WebSocket handler to block the handshake.

Impact: the browser's effective egress boundary is broader than the approved origin. This matters because web-page content is explicitly untrusted in section 15.

Fix target: install `RouteWebSocketAsync` before creating the page and block all WebSockets unless an adapter explicitly needs and authorizes one. Add a zero-handshake trap test. Review other browser channels that do not traverse request routing (for example WebRTC) and prefer a network-level deny-by-default boundary for future non-fixture use.

### P2 — Verified evidence binds only the resume, not the approved answer package

Before clicking, the browser checks visible input values. However, the page's untrusted submit handler controls the outgoing request and can replace fields after that check. Receipt validation checks only the receipt ID prefix, application key, and resume hash. The fake receipt already returns salary and professional-years values, but they are ignored; name and email are not represented in evidence.

Locations: `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:73`, `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:98`, `src/JobAgent.Core/Applications/ApplicationDraft.cs:43`.

Reproduction fixture: keep the DOM fields correct, but change the submit handler to send salary `1` while sending the approved resume and application key. If the response echoes the approved resume hash, the current code can return `SubmissionEvidence` and mark `SubmittedVerified` despite the changed answer.

Impact: `SubmittedVerified` does not prove that the answer package reviewed by the user was the package submitted.

Fix target: validate the one allowed outbound POST body against the approved payload at the request boundary and record the approved payload hash in `SubmissionEvidence`. For the synthetic adapter, also compare every receipt field the fake server echoes. Do not claim full-package verification on real sites that provide only a generic receipt.

### P2 — Sharing approval is persisted as unused after data transmission

`ShareAndFillFromUiAsync` stores a fresh sharing receipt and enters `Filling`. `PrepareAsync` calls `ApprovalPolicy.Validate`, not `Consume`, and no later update sets `Sharing.UsedAt`. The persisted journal therefore says the receipt is unused even after the page has received the fields and resume selection.

Locations: `src/JobAgent.Infrastructure/Applications/DemoWorkflow.cs:169`, `src/JobAgent.Infrastructure/Browser/ManagedBrowserSession.cs:30`.

Impact: the approval audit is inaccurate and the “single-use” property is not enforced for data-sharing consent. Recovery now correctly returns interrupted non-submission work to `ReadyForDataSharing`, but it preserves a receipt whose `UsedAt` incorrectly remains null.

Fix target: atomically consume the sharing receipt when claiming `ReadyForDataSharing → Filling`, before starting browser actions. Preserve the consumed receipt through success and recovery, and mint a new receipt for a new sharing attempt.

### P2 — `/api/state` returns the HttpOnly session identifier inside approval records

The HTTP cookie value is the `UiSession.Id`. That same ID is passed as `ApprovalReceipt.UserSessionId`. `GetStateAsync` returns the entire `WorkflowRecord`, so after an approval `/api/state` serializes the sharing/submission receipt and exposes `userSessionId` to JavaScript. This defeats the intended confidentiality of the HttpOnly cookie even though current React code ignores the extra property.

Locations: `src/JobAgent.Web/DashboardHost.cs:43`, `src/JobAgent.Web/DashboardHost.cs:65`, `src/JobAgent.Web/DashboardHost.cs:74`, `src/JobAgent.Infrastructure/Applications/DemoWorkflow.cs:90`.

Impact: any future same-origin script injection can read and exfiltrate the bearer session ID rather than being limited by HttpOnly. Approval IDs and internal timestamps are also unnecessary API surface.

Fix target: map workflow state to an explicit public response DTO. Omit `Sharing`, `Submission`, `UserSessionId`, and other authorization internals; return only the package summary, safe status, safe error code, and public receipt evidence required by the UI.

## Controls that held

- Dashboard requests reject wrong host/port before routing, enforce exact same-origin on every API write, require an authenticated cookie on all non-bootstrap API routes, and require the per-session CSRF header for writes.
- The session cookie is HttpOnly, SameSite Strict, path-scoped, and time-limited. The bootstrap token is passed in the fragment, removed from browser history before the API exchange, and compared in constant time.
- UI approval endpoints accept no `approved`, receipt, application, recipient, or file-path parameters. They bind approval creation to the authenticated server-side session.
- The managed browser validates the allowed loopback origin, synthetic target path, resume reference/hash, expected synthetic identity, form shape, and form values. Downloads and service workers are disabled, and no persistent browser profile, screenshots, or traces are enabled by default.
- Submission approval is separate from sharing approval and is durably claimed before the click. The standard response-drop path becomes `SubmittedUnverified` and remains non-retryable.

## Verification

Command:

```powershell
. .\scripts\use-toolchain.ps1
dotnet test JobAgent.slnx --no-restore --verbosity minimal
```

Results at the review snapshot:

- Core: 24 passed.
- Infrastructure: 15 passed.
- E2E: 27 passed, 2 failed, 29 total.
- The two failures are the redirect trap (`0` expected, `1` request observed) and network-drop request count (`1` expected, `2` POSTs observed). They were introduced by the concurrently added adversarial fixtures and directly substantiate the findings above.

The previously reported 65-test integrated baseline predates those three adversarial tests. No application or test file was edited during this review.

## Resolution re-review — 2026-09-18

This follow-up is a static review of the changed `ManagedBrowserSession`, `DemoWorkflow`, adversarial fixtures, multipart payload parser, submission evidence record, and public workflow-state projection. It applies the same loopback-fixture threat boundary as the original review. No build or test command was run for this follow-up.

| Prior finding | Status | Current evidence |
| --- | --- | --- |
| Redirect reaches a disallowed origin | Resolved | Every allowed HTTP request is performed with `Route.FetchAsync` using `MaxRedirects = 0` and `MaxRetries = 0`. Every 3xx response is aborted before it is fulfilled to Chromium. The redirect trap is reported green with zero requests at the second origin. |
| Preparation permits an early application POST | Resolved | Non-GET traffic is denied unless it is the exact `/api/applications` POST and atomically consumes the one-shot allowance opened only after the durable submission claim. The auto-submit-on-input fixture is reported green. |
| A dropped submission response causes a second POST | Resolved for the controlled fixture | The one-shot allowance is consumed before body parsing, transport retries are disabled, and the allowance is cleared in `finally`. The response-drop fixture is reported green with one POST and a durable `SubmittedUnverified` result. The general external-site limitation from section 8.4 still applies: a client cannot prove exactly-once acceptance without recipient-supported idempotency. |
| Malformed receipt strands `Submitting` or permits cancellation | Resolved | Receipt fields use object/property/type checks and return an unverified result on missing or wrong-type values. After a durable claim, `DemoWorkflow` catches every non-fatal outcome and its `finally` persists either `SubmittedVerified` or `SubmittedUnverified` before disposing the browser. Cancellation rejects `Submitting` and both submitted states. The missing-field and wrong-type fixtures are reported green. |
| WebSocket egress is not routed | Control implemented; focused verification pending | The context routes all WebSockets, never calls `ConnectToServer`, records the denial, installs a no-op close handler, and requests a policy close. The `blockedWebSocket` guard maps the observed Playwright 1.62 close-event `KeyNotFoundException` to a fail-closed policy result during preparation and suppresses that specific cleanup fault during browser disposal; `playwright.Dispose()` still runs in `finally`. This is a targeted workaround for Playwright 1.62's unguarded optional close fields, not a general exception suppression. |
| Evidence does not bind the approved answer package | Resolved | Before `FetchAsync`, the parser requires exactly the expected multipart part names, rejects duplicates and extra parts, compares every text value, checks the resume filename, and hashes the uploaded file bytes. The verified evidence stores `draft.PayloadHash()`, while the synthetic receipt checks the application key, resume hash, salary, professional years, and filename. The tampered-salary fixture is reported green with zero server POSTs. |
| Sharing approval remains unused | Resolved | The journal update atomically consumes the sharing receipt while moving `ReadyForDataSharing` to `Filling`; interrupted non-submission recovery therefore cannot reuse that consent. |
| UI state leaks the session identifier through approval records | Resolved | The public state projection includes only the draft, public evidence, and error for the application. It does not serialize either approval receipt or `UserSessionId`. The state fixture checks both the secret session value and the property name are absent. |

### Remaining actionable verification gap

**P2 test evidence — the WebSocket regression can pass without proving that the denial path ran.** `WebSocketEgress_IsBlockedBeforeHandshake` catches and discards any `PolicyException`, but it also succeeds when `PrepareAsync` completes normally as long as the trap counter remains zero. This matches the reported history: the test was already green before the WebSocket routing control was added, so it is not a RED reproduction of egress. Keep the zero-handshake assertion, and also require `PrepareAsync` to fail with `PolicyException.Code == "RecipientChanged"` (or expose a fixture-only observable denial signal). That makes the test prove both that the page attempted the socket and that the policy handler denied it.

No remaining payload, redirect, early-POST, retry, approval-consumption, uncertain-result, or session-disclosure bypass was identified by this static re-review within the loopback fixture boundary.

### Verification status for this follow-up

The parent run reported 34 of 35 E2E tests passing before the latest close-handler adjustment; the sole failure was the Playwright 1.62 WebSocket-route close-event parsing fault, not a demonstrated outbound handshake. `OnClose`/`CloseAsync` and the targeted cleanup guard were then added, and a focused run was still in progress when this review was written. These results are reported context, not commands executed by this reviewer.
