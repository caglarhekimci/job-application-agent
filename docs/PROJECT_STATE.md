# Project state

Updated: 2026-09-19 UTC (2026-09-20 Istanbul). Public repository was created at
https://github.com/caglarhekimci/job-application-agent. Initial source push, public CI
and release are being finalized. The user authorized public push and OSS application
submission and normal included Codex quota for required tests. Extra paid spending
is not authorized. Real job submissions still need a concrete selected package.

| Package | Status | Actual evidence / remaining work |
|---|---|---|
| W00-W01 | VerifiedLocal | Toolchain and synthetic career site; real Chromium |
| W02 | VerifiedLocal, partial | DPAPI profiles, version conflicts, migrations and atomic deletion; see storage evidence |
| W03 | VerifiedLocal | TXT/PDF/DOCX worker, 15 parser tests; local review and protected original; packaged PDF/DOCX smoke passed; OCR absent |
| W04-W05 | VerifiedLocal, partial | Reviewed manual posting, permissions, deterministic answers; model proposals and new-answer scope UI remain |
| W06-W08 | VerifiedLocal, partial | G1 achieved; separate approval/claim/receipt; personal CV/job review UI; generalized live forms remain |
| W09 | VerifiedLocal, partial | 12 STDIO tests plus one actual Codex capability call; complete host-mediated application remains |
| W10 | InProgress | 11-case fixture runner; broader benchmark and model-quality comparison remain |
| W11 | VerifiedLocal, partial | Negative browser/request/parser/storage tests; final scope in threat model |
| W12 | InProgress | 137 integrated tests, launcher, package/hash and PDF/DOCX smoke passed; fresh checkout and public CI pending |
| W13 | BlockedExternal | No user-reviewed real CV, selected live target or platform permission; personal-use screen exists |
| W14 | Authorized, InProgress | Public repo created; code push/release pending |
| W15 | External evidence pending | 0 independent users; no fabricated adoption |
| W16 | Authorized, InProgress | Private applicant fields obtained, Pro/Codex-only form being prepared; final terms confirmation and receipt pending |
| W17 | Local package verified | Portable/Codex manifest and relocated STDIO smoke; store publication blocked on documented publisher/hosting/review gates |
| W18 | Deferred optional | No measured need or paid fine-tuning budget |

Latest integrated verification: `scripts/verify.ps1`, exit 0,
`artifacts/verification/20260919T205804Z/`: Core 42, Document 15, E2E 36,
Infrastructure 25, MCP 12, Workspace 7 = **137 passed; 0 failed; 0 skipped**.
Build: 0 warnings/errors. Launcher, fixture evaluation, four scanner regressions,
source/history scan and whitespace check passed. Evidence distinguishes actual
model-host invocation from fixture quality; no paid model API is used.

The new personal workspace is separate from the synthetic submission journal.
It stores protected resume/profile/version/job state in `personal/workspace.db`
outside the checkout. It never fetches the pasted URL or submits to an employer.
Local user review creates verified facts; a changed document clears confirmations.
Review fixes preserve unsaved fields across tabs and nullable private salary data.

All private applicant contact/organization values are outside the repository.
No API key, purchase, billing change, real employer application or store acceptance
has occurred. LinkedIn remains blocked. Program benefit is discretionary.

Resume from this file, `VERIFICATION.md`, `CAPABILITY_MATRIX.md` and
`evidence/remaining-deliverables.md`. Complete exact-commit packaging/clean checkout,
public push/CI/release, then the prepared OSS form's action-time terms gate. Expand
the incomplete local W05/W07/W09/W10 slices afterward; do not mark them complete
merely because the initial public prototype is published.
