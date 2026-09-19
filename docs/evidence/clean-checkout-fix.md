# Deterministic late-challenge coordinator regression

Verified 2026-09-19 UTC (2026-09-20 Istanbul).

The clean checkout of `5b65ed7` failed one E2E test: 83 passed, 1 failed,
0 skipped. `LateChallengeBeforeClaim_PausesAndClearsStoredSubmissionApproval`
assumed that a 750ms MFA timer would fire after UI approval. Under load it fired
during approval instead. The test ignored that result; later execution correctly
raised `NeedsInput`. This was a test ordering defect, not permission to weaken the
production state or manual-challenge checks.

Original evidence is preserved locally under
`artifacts/test-results/clean-checkout-fix/original-clean-red.log` and
`original-clean-red.trx`, copied from
`%TEMP%/jobagent-clean-da84c25/artifacts/verification/20260919T231226Z/`.
The TRX SHA256 is
`55b232a99d208183334c7f2c774509e111b1ce0669713e54ce020f17dfc7d238`.
The original `scripts/verify.ps1` exit was **1**.

The corrected coordinator test wraps the actual `ManagedBrowserSession` through
the existing `IBrowserSession` contract. It prepares and validates the real
Chromium form, asserts successful UI approval and an unused durable receipt,
then explicitly activates a simulated `ManualTakeoverRequired` event at the next
readiness check. It asserts `NeedsInput`, cleared submission approval, disposed
browser, no call to browser submission, no evidence and **zero target POSTs**.
There is no timer or sleep in this test. Separate browser-policy tests continue to
exercise actual MFA/CAPTCHA DOM detection.

The only production change is an optional trusted constructor factory whose
default remains `ManagedBrowserSession`; the workflow field uses `IBrowserSession`.
No new browser/UI/MCP endpoint or permission is exposed. Approval, durable claim,
expiry and manual-takeover policy behavior is unchanged.

Test-first record:

1. The revised test initially failed compilation with CS1739 because the trusted
   factory seam did not yet exist: exit **1**, `seam-red.log`. This is a compile
   RED; the original clean-checkout failure above is the observed runtime RED.
2. After adding the factory, the focused E2E command exited **0**:
   **13 passed, 0 failed, 0 skipped**. Reports: `focused-green.log` and
   `focused-green.trx` in the same evidence directory.

Commands used with the per-user .NET SDK:

```powershell
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --filter 'FullyQualifiedName~LateChallengeBeforeClaim' --logger 'trx;LogFileName=seam-red.trx' --results-directory artifacts/test-results/clean-checkout-fix
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --filter 'FullyQualifiedName~HostCoordinatorTests|FullyQualifiedName~BrowserPolicyTests.ManualChallenge|FullyQualifiedName~BrowserPolicyTests.ChallengeAppearing|FullyQualifiedName~AdversarialFormTests.ManualChallenge|FullyQualifiedName~ResumableSynthetic' --logger 'trx;LogFileName=focused-green.trx' --results-directory artifacts/test-results/clean-checkout-fix
```

The focused run includes all six host coordinator tests, the four real-DOM
manual-challenge cases and all three resumable synthetic workflow/UI tests.
It is not a replacement for the next complete exact-commit checkout run.
No model, paid API or real employer submission was used.
