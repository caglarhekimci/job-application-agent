# Capability matrix

Local integration (2026-09-20): **255 tests passed**, including typed synthetic
controls, persistent question review, local12 STDIO and protected recovery/audits.
See the package-specific W07/W08/W09/store evidence. A new exact-commit package,
public CI remain separate verification steps. The actual-model local12 scenario
passed two turns and twelve tools with zero target requests; the first clean-checkout
run caught a timing-dependent challenge test, which is being corrected before release.

| Capability | Status | Boundary |
|---|---|---|
| Local synthetic workflow | VerifiedLocal | Actual UI + Chromium + receipt; no employer receives data |
| Verified candidate memory | VerifiedLocal | DPAPI/versioned SQLite, all-store migrations/audits, protected snapshots/restore, deletion and source-review UI tested |
| Answer/approval engines | VerifiedLocal | Deterministic fields; strict model proposal validation; scoped memory review/revocation; separate UI-only consent |
| Managed Playwright browser | VerifiedLocal | Typed text/select/checkbox/radio/file/conditional steps on fixed synthetic loopback origin; adverse request and CAPTCHA/MFA stops |
| MCP STDIO | VerifiedLocal | Default3, opt-in synthetic6 or local12; 22 protocol tests plus real STDIO sequences; no approval tool |
| Fixture evaluation | VerifiedLocal | Initial11 plus12profiles/240questions/60jobs; real numerators/denominators; not model quality or independent holdout |
| TXT/PDF/DOCX import | VerifiedLocal | 15 parser tests and packaged PDF/DOCX smoke; OCR absent |
| Personal profile/job UI | VerifiedLocal | Protected CV/job/application drafts, explicit source and question review, scoped answers and pending host proposals; no employer connection |
| Real model | VerifiedSyntheticPilot | Three-turn Codex workflow with simulated UI approval/one receipt; six-turn24-question comparison B1 69/72 vs B2 57/72; no B2 improvement or live-site claim |
| LinkedIn search/autofill/submit | BlockedExternal | No verified platform permission |
| Authorized live career site | BlockedExternal | No selected/authorized live target |
| GitHub publication | PublicSourceAndRelease | First source da84c25, v0.1.0-alpha.1 at b068723; private vulnerability reporting enabled |
| CI | Passed for historical checkpoint | GitHub Actions35472502796 and clean local checkout passed188 tests at ac53072; next integration CI pending |
| OSS grant | SubmittedConfirmed | OpenAI success page observed; private receipt retained; selection/benefit unknown |
| Local plugin package | VerifiedLocalAndIsolatedHost | Portable/Codex manifests, relocated STDIO and actual isolated host installation; store publication remains externally gated |
