# Threat model — local synthetic prototype

Assets: confirmed profile facts, private salary minimum, CV bytes, approval records,
session material, application status and submission integrity. Untrusted inputs:
page scripts, documents, job text, MCP arguments and foreign-origin web requests.
Trusted: installed application code, the Windows user and approved fixture setup.
This does not defend against arbitrary code execution as the same OS user.

Implemented controls:

- Loopback-only dashboard, exact Host/port checks, same-Origin API writes,
  HttpOnly/SameSite Strict cookie, CSRF token, CSP, safe errors and no request logs.
- Proposed/edited profile facts cannot silently inherit confirmation. Sensitive
  payloads are protected at rest. Manual-only and unknown answers do not fabricate.
- Sharing and submission receipts bind package hash, recipient, profile version,
  resume hash, session and expiry. Receipts are consumed durably before each stage.
- Managed browser accepts one literal loopback origin and fixed synthetic route.
  HTTP requests are fetched without redirects or retry. Mutation requires a
  one-shot final-submit gate and exact multipart fields/file hash; WebSockets
  never connect to a server. Downloads and service workers are disabled.
- A durable conditional claim allows only one submission attempt. Unknown results
  remain uncertain after restart. Cancellation cannot relabel a claimed attempt
  as if it were safely withdrawn. The fake server also deduplicates application IDs.
- MCP is read-only, has strict arguments, accepts GUID references rather than paths,
  returns minimal summaries and never exposes approval or browser tools.

Known limits and release gates:

- This is application-level browser policy, not an OS network firewall. WebRTC,
  arbitrary hostile external sites and every browser channel are not certified.
  Any live adapter requires a stronger egress review and platform authorization.
- Native host/browser tools bypass this managed runtime. No control over their
  actions is claimed (`NativeHostBypassesManagedRuntime`).
- Playwright 1.62 can throw KeyNotFoundException on routed WebSocket close events
  with absent optional fields. The runtime maps that fault to a stop only after
  denial and tolerates it during cleanup; the driver is always disposed. A zero
  handshake trap regression covers the deny boundary. This is a pinned SDK issue.
- Full PDF/DOCX parsing, migration upgrades, audit-chain retention, multiple users,
  authentication rate limiting and a protected real-application journal remain
  unfinished. Current runtime is for a single user's synthetic fixture only.
- Exactly-once delivery to arbitrary employers is not promised. Local claim and
  fixture server deduplication cannot settle an unknown external outcome.
- No public vulnerability channel, independent security audit or clean-user/CI
  verification exists yet. These are not replaced by local test success.

See docs/evidence/integrated-review.md for reproduced defects and follow-up review;
docs/VERIFICATION.md records executed regression results. G2 is partial, not a
claim that every negative case in the master plan has been covered.
