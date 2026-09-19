# Capability matrix

Local integration (2026-09-20): **293 distinct tests pass** across the final
clean-checkout sequence and one targeted UI correction. The original failed run
is retained; see [execution details](evidence/final-integration.md). Package/CI
results are separate. The actual-model local12 scenario passed two turns and
twelve tools with zero target requests on its recorded build.

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
| CV/letter adaptation (FR-15) | VerifiedLocal | Exact verified source selection/reordering, comparison, bound approval and UTF-8 text export; no generative rewrite or automatic attachment replacement |
| Runtime proposal policy (FR-16) | VerifiedLocal | Fixture/HostMediated, durable per-application accepted-operation limit1-10; no paid API, host-model selection or account-token/quota enforcement |
| Real model | VerifiedSyntheticPilot | Three-turn Codex workflow with simulated UI approval/one receipt; six-turn24-question comparison B1 69/72 vs B2 57/72; no B2 improvement or live-site claim |
| LinkedIn search/autofill/submit | BlockedExternal | No verified platform permission |
| Authorized live career site | BlockedExternal | No selected/authorized live target |
| GitHub publication | PublicSourceAndRelease | First source da84c25, v0.1.0-alpha.1 at b068723; private vulnerability reporting enabled |
| CI | Passed for historical checkpoint | GitHub Actions35475408596 passed at5b65ed7; exact alpha.2 result recorded in release evidence |
| OSS grant | SubmittedConfirmed | OpenAI success page observed; private receipt retained; selection/benefit unknown |
| Local plugin package | VerifiedLocalAndIsolatedHost | Portable/Codex manifests, relocated STDIO and actual isolated host installation; store publication remains externally gated |
