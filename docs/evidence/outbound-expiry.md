# Approval and answer expiry at the outbound boundary

Verified 2026-09-19 UTC (2026-09-20 Istanbul), using synthetic data and real Chromium.

Review found that the browser validated submission consent before asynchronous
form checks and the click, while the final routed POST checked only its one-use
allowance and exact body. An approval or answer could expire during a page delay.

The regression fixture adds an optional trusted callback behind a fixed same-origin
`/test/submit-ready` GET. Only tests supplying that callback get the gate. After the
real click reaches it, the test advances an injected clock and releases the page
to make its real POST. No fixed sleep, arbitrary browser action or external URL is
used. The gate grants no submission permission.

Observed RED, before changing expiry behavior:

- Core: **6 failed, 1 passed**, exit **1** (`core-red.log` / `core-red.trx`).
- Browser: **2 failed, 1 passed**, exit **1** (`browser-red.log` / `browser-red.trx`).
  Both expired cases actually reached **1 POST**, where the test required zero.
  The valid control completed its single submission.

The fix carries the earliest selected answer-memory expiry and cited fact validity
deadline into `AnswerResolution.ValidUntil`. The resolved application stores the
earliest deadline as nullable `AnswersValidUntil`, included in its package hash.
UI approvals expire at the earlier of that deadline and ten minutes. Validation
also checks the package deadline independently of the receipt expiry. Missing
legacy deadlines deserialize as null; existing approvals cannot authorize a
changed package hash.

The managed browser captures the active immutable submission receipt and checks
it, the package deadline and cancellation immediately before `Route.FetchAsync`,
after exact multipart validation. GET requests and each preparation action also
recheck sharing consent. A blocked POST has no verified receipt; the workflow's
conservative handling of an already claimed submission remains unchanged.

Observed GREEN:

- Core project: **71 passed, 0 failed, 0 skipped**, exit **0** (`core-green.log` /
  `core-green.trx`), including all seven new deadline cases.
- Focused browser/coordinator/resume set: **20 passed, 0 failed, 0 skipped**, exit
  **0** (`browser-green.log` / `browser-green.trx`). Both expired scenarios reached
  **zero POSTs**; the still-valid control reached **one POST** with verified evidence.

All reports are under ignored `artifacts/test-results/outbound-expiry/`.
Commands, with the per-user .NET SDK:

```powershell
dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore --filter 'FullyQualifiedName~AnswerDeadlineTests' --logger 'trx;LogFileName=core-red.trx' --results-directory artifacts/test-results/outbound-expiry
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --filter 'FullyQualifiedName~OutboundApprovalTests' --logger 'trx;LogFileName=browser-red.trx' --results-directory artifacts/test-results/outbound-expiry
dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore --logger 'trx;LogFileName=core-green.trx' --results-directory artifacts/test-results/outbound-expiry
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --filter 'FullyQualifiedName~OutboundApprovalTests|FullyQualifiedName~BrowserPolicyTests|FullyQualifiedName~HostCoordinatorTests|FullyQualifiedName~ResumableSynthetic' --logger 'trx;LogFileName=browser-green.trx' --results-directory artifacts/test-results/outbound-expiry
```

No model call, paid API, real employer submission or account setting change was
used. Complete integrated clean-checkout verification is recorded separately.
