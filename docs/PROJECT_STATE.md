# Project state

Updated: 2026-09-19 UTC (2026-09-20 Istanbul). Public repository was created at
https://github.com/caglarhekimci/job-application-agent. First public source was
da84c258362402e6feed898cfd5f02224af9b7c3; clean-checkout verification and public CI
passed. Prerelease v0.1.0-alpha.1 is published at b068723; see evidence/release-alpha1.md. The user authorized public push and OSS application
submission and normal included Codex quota for required tests. Extra paid spending
is not authorized. Real job submissions still need a concrete selected package.

Current public checkpoint before the next push: `b7f020d` (bilingual quick start).
The next integration adds W07 typed extended controls, W08 persistent questions
and personal review UI, W09 shared 12-tool local mode, and protected migration
recovery/audits. Review findings for expiry, legacy scope identity, stale proposals,
deduplication and UI concurrency have regression tests. Combined Release tests:
**255 passed, 0 failed, 0 skipped**, under `artifacts/verification/20260919T230618Z/`.
The new actual model local-mode scenario and exact-commit package/CI are pending;
they are separate from the completed 188-test historical checkpoint.

| Package | Status | Actual evidence / remaining work |
|---|---|---|
| W00-W01 | VerifiedLocal | Toolchain and synthetic career site; real Chromium |
| W02 | VerifiedLocal | DPAPI profiles/workspace, versions, all-store schema contracts, protected pre-upgrade snapshots, explicit restore and metadata audits; see store-recovery.md |
| W03 | VerifiedLocal | TXT/PDF/DOCX worker, 15 parser tests; local review and protected original; packaged PDF/DOCX smoke passed; OCR absent |
| W04-W05 | VerifiedLocal | Reviewed manual posting, permissions, deterministic answers, strict model proposal validator and reviewed scoped-memory UI/revocation; model proposals never self-approve |
| W06-W08 | VerifiedLocal | G1, typed select/checkbox/radio/conditional forms, persisted unanswered questions, reviewed scoped memory and same-application resume; browser limited to synthetic site |
| W09 | VerifiedLocalAndCodex, partial | Default3 / synthetic6 / local12 modes; UI and local12 share protected services; actual STDIO12 passed; fresh model12 scenario pending, legacy synthetic model workflow passed |
| W10 | VerifiedLocalAndModelPilot | Initial11 + expanded12 profiles/240 questions/60 jobs; B0 passes; real six-turn24-question pilot B1 69/72 vs B2 57/72 strict; B2 did not improve |
| W11 | VerifiedLocal, partial | Negative browser/request/parser/storage tests; final scope in threat model |
| W12 | VerifiedLocalAndCI, package update pending | Latest255 local tests passed; historical188 passed clean checkout and exact public CI at ac53072; new exact-commit app/plugin package pending |
| W13 | BlockedExternal | No user-reviewed real CV, selected live target or platform permission; personal-use screen exists |
| W14 | Public source verified | main pushed; private vulnerability reporting enabled; v0.1.0-alpha.1 published with app/plugin ZIPs |
| W15 | External evidence pending | 0 independent users; no fabricated adoption |
| W16 | SubmittedAwaitingDecision | Submitted and page-confirmed after explicit terms approval; Pro/Codex only; decision pending |
| W17 | Local package and isolated host verified | Portable/Codex manifests; actual installed-plugin8 scenarios plus targeted follow-up; store publication blocked on documented publisher/hosting/review gates |
| W18 | Deferred optional | No measured need or paid fine-tuning budget |

Previous integrated verification: `scripts/verify.ps1`, exit 0,
`artifacts/verification/20260919T215755Z/`: Core 58, Document 15, E2E 59,
Infrastructure 32, MCP 14, Workspace 10 = **188 passed; 0 failed; 0 skipped**.
Build: 0 warnings/errors. Launcher, fixture evaluation, four scanner regressions,
source/history scan and whitespace check passed. Evidence distinguishes actual
model-host invocation from fixture quality; no paid model API is used.

The same 188 tests passed from a separate clean checkout of
`ac530725c7653f7980acc10e90c213092afc6418`; reports in that clone:
`artifacts/verification/20260919T220948Z/`. Offline model replay also passed
12 assertions; this replay is not a fresh model run. Exact-commit public CI
[35472502796](https://github.com/caglarhekimci/job-application-agent/actions/runs/35472502796)
completed successfully. The checkout reused the current user's dependency/browser
caches; a fresh interactive Windows user setup is not claimed.

README now provides English followed by Turkish quick-start instructions, explicit
prerequisites and the fact that Chrome/desktop-control extensions are unnecessary.
Installation commands were checked against the actual scripts and clean-checkout
verification. Optional Codex integration is separate from the free local workflow.

The new personal workspace is separate from the synthetic submission journal.
It stores protected resume/profile/version/job state in `personal/workspace.db`
outside the checkout. It never fetches the pasted URL or submits to an employer.
Local user review creates verified facts; a changed document clears confirmations.
Review fixes preserve unsaved fields across tabs and nullable private salary data.

All private applicant contact/organization values are outside the repository.
No API key, purchase, billing change, real employer application or store acceptance
has occurred. LinkedIn remains blocked. Program benefit is discretionary.

Resume from this file, `VERIFICATION.md`, `CAPABILITY_MATRIX.md` and current
`evidence/w09-bridge.md`, `evidence/codex-workflow.md`, `evidence/w10-expanded-fixtures.md`.
The [local acceptance audit](evidence/local-acceptance-audit.md) is a historical
read-only review; its three P0 gaps are addressed by this integration. The feasible
P1 document adaptation/diff (FR-15) and runtime budget selection (FR-16) are next.
Publish the verified next package after integration. Earlier remaining-deliverables.md is a historical
audit, not the current state. Do not mark all long-term packages complete because
the public prototype and grant submission exist.
