# Changelog

## Unreleased — next tested local checkpoint

- Added reviewed application/company/global answer memory, expiry, revision checks
  and revocation; strict model proposals remain subject to user review.
- Added CAPTCHA/MFA stops, protected local companion/MCP bridge and three opt-in
  synthetic workflow commands. Consent remains a separate browser UI operation.
- Added actual UI/STDIO workflow coverage, single-POST assertions, bridge response
  validation, redirect refusal and stale registration cleanup.
- Fixed packaged plugin launcher selection when several dotnet paths are present.
- Expanded B0 to 12 profiles, 240 questions and 60 jobs. A separate six-turn Codex
  pilot found B1 better than B2 on the frozen strict metric; no improvement claimed.
- Ran 188 local tests and an actual three-turn Codex synthetic application workflow.
  Simulated UI approval in the latter is explicitly separated from real human approval.
- Submitted Codex for OSS after explicit terms acceptance; selection is pending.

## v0.1.0-alpha.1 — 2026-09-19 UTC

- Added versioned profile facts, DPAPI storage and bounded TXT import.
- Added typed job evaluation and deterministic answers with safe abstention.
- Added separate UI sharing/submission approvals and durable submission attempts.
- Added a real Chromium workflow, file upload and verified synthetic receipt.
- Fixed redirect-before-check, early script POST, network retry, outgoing answer
  tampering, malformed receipt handling and state-response session disclosure.
- Added a read-only STDIO MCP server with real subprocess protocol tests.
- Added setup, verification, source/history scan and local packaging scripts.

- Added protected personal CV/profile/job review, PDF/DOCX worker imports and export/deletion.
- Verified one actual read-only Codex capability call and a relocated local plugin package.
- Published source with passing Windows CI and a clean-checkout 137-test result.

No live adapter, model-quality benchmark, independent adoption or store approval is claimed.
