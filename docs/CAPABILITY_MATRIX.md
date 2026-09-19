# Capability matrix

| Capability | Status | Boundary |
|---|---|---|
| Local synthetic workflow | VerifiedLocal | Actual UI + Chromium + receipt; no employer receives data |
| Verified candidate memory | VerifiedLocal, partial | DPAPI/versioned SQLite, migrations, atomic deletion and personal source-review UI tested |
| Answer/approval engines | VerifiedLocal | Deterministic fields; strict model proposal validation; scoped memory review/revocation; separate UI-only consent |
| Managed Playwright browser | VerifiedLocal, partial | Fixed synthetic loopback origin; adverse request and CAPTCHA/MFA stop tests pass; general controls remain |
| MCP STDIO | VerifiedLocal, partial | 14 focused tests plus UI/STDIO6-tool full workflow; three writes explicitly opt-in, no approval tool |
| Fixture evaluation | VerifiedLocal | Initial11 plus12profiles/240questions/60jobs; real numerators/denominators; not model quality or independent holdout |
| TXT/PDF/DOCX import | VerifiedLocal | 15 parser tests and packaged PDF/DOCX smoke; OCR absent |
| Personal profile/job UI | VerifiedLocal, partial | Protected CV, explicit source review, pasted job, preview, export/delete; no employer connection |
| Real model | VerifiedSyntheticPilot | Three-turn Codex workflow with simulated UI approval/one receipt; six-turn24-question comparison B1 69/72 vs B2 57/72; no B2 improvement or live-site claim |
| LinkedIn search/autofill/submit | BlockedExternal | No verified platform permission |
| Authorized live career site | BlockedExternal | No selected/authorized live target |
| GitHub publication | PublicSourceAndRelease | First source da84c25, v0.1.0-alpha.1 at b068723; private vulnerability reporting enabled |
| CI | Passed | GitHub Actions run 35469458955 on da84c25; clean local checkout also passed all 137 tests |
| OSS grant | SubmittedConfirmed | OpenAI success page observed; private receipt retained; selection/benefit unknown |
| Local plugin package | VerifiedLocalAndIsolatedHost | Portable/Codex manifests, relocated STDIO and actual isolated host installation; store publication remains externally gated |
