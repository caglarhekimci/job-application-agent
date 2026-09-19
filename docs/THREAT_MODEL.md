# Threat model — local synthetic prototype

Assets include confirmed profile facts, private salary minimum, original CV bytes, answer memory, approval records, UI and bridge credentials, application status and submission integrity. Untrusted inputs include page scripts, TXT/PDF/DOCX documents, pasted job text, model output, MCP arguments and foreign-origin web requests. Installed application code, the Windows user and the approved fixture setup are trusted. The prototype does not defend against arbitrary code execution as the same OS user.

## Implemented controls

- The dashboard binds literal loopback and requires an exact Host and port. API writes require the expected Origin, an HttpOnly/SameSite Strict session cookie and CSRF token. Responses use CSP, no-store, no-referrer, safe errors and no request logging.
- Profile facts imported from documents remain proposed until local review. Edits do not silently inherit confirmation. W05 answer memory is bound to global, employer or exact-application scope with revision checks, expiry, history and explicit revocation. Model answer proposals are schema validated, evidence bound and remain unverified until review.
- TXT is decoded with a strict byte cap. PDF and DOCX are parsed in a killable worker with input, output, page, character, archive-expansion and timeout bounds. DOCX macros, embedded active content, external relationships, traversal-like paths and DTDs are rejected. Scan-only PDFs return `NeedsOcr` rather than invented facts. The worker boundary is not an OS sandbox or hard memory quota.
- Profile storage uses current-user Windows DPAPI and a tested version 0/1/2 migration path with metadata-only audit entries. Job, application-journal and personal-workspace stores still use initial `EnsureCreated` schemas and do not yet have general upgrade chains.
- Sharing and submission receipts bind application, payload hash, recipient, profile version, resume hash, UI session, purpose and expiry. Submission approval lasts ten minutes and is consumed by an atomic durable claim. A failed or interrupted result after claim remains uncertain and is never retried automatically.
- Managed Chromium accepts one literal synthetic loopback origin and fixed routes. Requests are fetched without redirect or retry. The final POST requires the exact approved multipart parts and file bytes. WebSockets never connect to a server; downloads and service workers are disabled.
- W07 detects bounded semantic CAPTCHA and MFA markers before preparation and immediately before claim/click. A pre-claim challenge clears approval and pauses as `ManualTakeoverRequired`; it is never solved or bypassed. A post-claim fault remains `SubmittedUnverified`.
- W09 exposes three read-only STDIO tools by default. Exactly three fixed-synthetic command tools appear only when `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1` is present in both companion and MCP processes. Commands accept only canonical GUID references and cannot accept approval, credentials, paths, URLs, files, answers, browser instructions or shell input.
- The command bridge uses a separate 256-bit token in a current-user DPAPI registration, literal loopback, exact Host/port, no Origin, no query/body and an eight-hour maximum registration lifetime. UI credentials cannot command the bridge, and bridge credentials cannot use UI approval routes. The client disables proxy, cookies, redirects and retries; malformed success responses and ambiguous execution failures fail closed.
- MCP execution can consume only an unexpired approval already stored by the authenticated UI. Repeated and concurrent execution converge on the same durable attempt. On transport or post-claim uncertainty, callers must read status instead of resubmitting.

## Known limits and release gates

- Browser controls are application policy, not an OS network firewall. WebRTC, hostile external sites and every browser channel are not certified. Native host/browser tools bypass this managed runtime (`NativeHostBypassesManagedRuntime`).
- Playwright 1.62 can throw `KeyNotFoundException` when routed WebSocket close events omit optional fields. The runtime tolerates only the known cleanup shape after denying the socket and always disposes the driver; a zero-handshake regression covers the boundary.
- The CAPTCHA/MFA behavior is a fail-closed pause. It does not provide an interactive manual browser handoff or same-page resume.
- The MCP command path is fixed to the bundled synthetic profile, job, CV and career site. It is not the planned general 12-tool service, a remote MCP transport or a live-site adapter. LinkedIn automation remains blocked without platform authorization.
- Local claim plus fixture deduplication does not promise exactly-once delivery to arbitrary employers. An unknown external result would still require human reconciliation.
- PDF/DOCX tests use synthetic documents. Real-world corpus, fuzzing, parser-vulnerability review and a kernel sandbox remain future hardening.
- Profile migrations are tested for the implemented versions; other SQLite stores need explicit upgrade plans before their schemas change. Multiple users and UI authentication rate limiting remain outside this local single-user prototype.
- Private vulnerability reporting is enabled and documented in `SECURITY.md`; no independent security audit or independent adoption is claimed.

## Evidence boundary

The public baseline commit passed 137 tests locally, in a clean checkout and in GitHub Actions. That remains the exact count for that commit. After the W05, W07 and W09 additions, the current workspace verification passed 188/188 with zero skipped tests and zero build warnings; reports are under `artifacts/verification/20260919T215755Z`. The W09 bridge/UI review also has a separate 20/20 focused report in `docs/evidence/w09-bridge.md`. Neither local result is an independent security audit or a live-site success measurement.

See `docs/evidence/integrated-review.md`, `docs/evidence/document-import.md`, `docs/evidence/storage-migrations.md`, `docs/evidence/w07-manual-takeover.md`, `docs/evidence/w09-bridge.md` and `docs/VERIFICATION.md` for executed evidence and remaining limits.
