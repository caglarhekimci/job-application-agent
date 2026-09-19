# W07 CAPTCHA/MFA manual-takeover boundary

**Implemented and focused-test date:** 2026-09-20
**Scope:** synthetic local fixture and managed Playwright session only. No live site, account, CAPTCHA service, MFA code, external network target, or personal data was used.

## Behavior

The managed browser recognizes a narrow set of visible CAPTCHA and MFA signals:

- CAPTCHA iframe title/source attributes and `data-sitekey`;
- one-time-code autocomplete fields;
- input names containing OTP, MFA, or verification-code identifiers.

Detection runs immediately after navigation, before and after each managed preparation action, during form validation, in the workflow preflight before a submission approval/claim is created, and again inside `SubmitAsync` immediately before its one allowed submit click. A detected signal throws the fixed policy code `ManualTakeoverRequired`. The implementation does not fill the challenge, click it, reload it, call a solver, extract secrets, or add any network origin.

The workflow reuses `ApplicationStatus.NeedsInput` as the durable paused state when detection occurs before a submission claim. It stores `ManualTakeoverRequired`, disposes the headless browser, leaves `Submission` null, and blocks submission after restart. If a challenge appears only after a durable submission claim has already been consumed, the existing uncertain-result rule remains authoritative: the workflow must not rewrite that attempt as a safe pause or retry it.

Disposing the browser is a fail-closed pause. This change does **not** implement a visible interactive browser handoff, challenge completion, or resume-from-the-same-page feature. A later authorized UI design would be required for that experience.

## Synthetic fixture and tests

`FakeCareerOptions` can render either a synthetic CAPTCHA iframe or a synthetic MFA one-time-code field. It can also add the challenge after the resume input changes, which exercises detection between form preparation and final submission.

The TDD RED run failed at compilation because `ManualChallengeKind`, the fixture options, and `EnsureReadyForSubmissionAsync` did not exist. After implementation, the focused command passed **4/4**:

```powershell
. ./scripts/use-toolchain.ps1
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~ManualChallenge|FullyQualifiedName~ChallengeAppearingAfterPreparation" `
  --verbosity minimal
```

The cases prove:

- initial CAPTCHA and MFA signals stop preparation with the exact policy code and zero submission POSTs;
- a challenge appearing after preparation is detected by `SubmitAsync` before its final click, with zero submission POSTs;
- workflow preflight detects a late challenge before minting/consuming submission approval, persists `NeedsInput` plus `ManualTakeoverRequired`, keeps `Submission` null, blocks submit, and preserves the pause across restart.

## Broader-run note

A subsequent full Debug E2E attempt encountered the previously observed upstream Playwright WebSocket cleanup instability: `InvalidOperationException: No process is associated with this object` from `Playwright.Dispose` in `WebSocketEgress_IsBlockedBeforeHandshake`. The isolated WebSocket regression immediately passed **1/1**. A second broad run overlapped another agent's Release E2E process and was not treated as release evidence. Therefore this note claims the focused **4/4** result only; the repository's final coordinated verification ledger must supply the full-project result.

## Limits

- The selector set intentionally avoids broad page-text matching, which would create false positives. An unfamiliar challenge without the recognized semantic attributes may instead fail the fixed-form validation as `FormChanged`.
- DOM checks reduce the race window but cannot prove that a page will not change between the last check and an action. The durable submission claim and unknown-result handling remain the backstop after submission starts.
- This is detection and pausing, not challenge solving or bypass. Live-site behavior remains blocked by source authorization and the existing recipient/origin policy.
