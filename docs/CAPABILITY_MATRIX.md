# Capability matrix

| Capability | Status | Boundary |
|---|---|---|
| Local synthetic workflow | VerifiedLocal | Actual UI + Chromium + receipt; no employer receives data |
| Verified candidate memory | VerifiedLocal, partial | DPAPI/versioned SQLite, migrations, atomic deletion and personal source-review UI tested |
| Answer/approval engines | VerifiedLocal, partial | Deterministic fields; separate UI-only consent; model extraction pending |
| Managed Playwright browser | VerifiedLocal, partial | Fixed synthetic loopback origin; adverse request tests pass |
| MCP STDIO | VerifiedLocal, partial | 12 tests including official-client subprocess; one actual Codex capability call verified |
| Fixture evaluation | VerifiedLocal, partial | 11 cases, real numerators/denominators; not model quality or independent holdout |
| TXT/PDF/DOCX import | VerifiedLocal | 15 parser tests and packaged PDF/DOCX smoke; OCR absent |
| Personal profile/job UI | VerifiedLocal, partial | Protected CV, explicit source review, pasted job, preview, export/delete; no employer connection |
| Real model | CapabilitySmokeOnly | Actual Codex call verified; quality evaluation and complete host workflow NotRun |
| LinkedIn search/autofill/submit | BlockedExternal | No verified platform permission |
| Authorized live career site | BlockedExternal | No selected/authorized live target |
| GitHub publication | PublicSourceVerified | main commit da84c25 public; private vulnerability reporting enabled; release pending |
| CI | Passed | GitHub Actions run 35469458955 on da84c25; clean local checkout also passed all 137 tests |
| OSS grant | Prepared, NotSubmitted | Private fields obtained; Pro/Codex-only request; final terms gate pending; no award guarantee |
| Local plugin package | VerifiedLocal | Portable/Codex manifests and relocated STDIO smoke; store publication remains externally gated |
