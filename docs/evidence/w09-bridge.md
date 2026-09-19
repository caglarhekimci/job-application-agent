# W09 host bridge and synthetic-command evidence

**Verified:** 2026-09-20
**Runtime:** Windows, .NET 10.0.401, official `ModelContextProtocol` 2.2.0 SDK, Codex CLI 0.155.0 for the separate host capability smoke
**Scope:** fixed synthetic profile, job, CV, loopback dashboard and fake career site only

## Shipped boundary

The STDIO MCP remains read-only by default and lists three tools:

1. `runtime_get_capabilities`
2. `profile_get_summary`
3. `application_get_status`

Exactly three additional commands are registered only when the MCP process has `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1`:

1. `application_create_draft`
2. `application_prepare_review`
3. `application_execute_approved`

The companion must independently start with the same opt-in. Without it, it publishes no bridge registration and rejects internal commands. Both processes must use the same `JOBAGENT_RUNTIME_DIR`.

The commands expose only the fixed synthetic workflow. They accept no approval value, session, profile/contact data, answer, file, path, URL, recipient, browser action or shell input. Their response contains only application reference, state, review-requested, currently-valid submission-approved and verified receipt ID. Unknown MCP arguments are rejected before dispatch.

## Credential and approval separation

The companion creates a random bridge credential distinct from its UI bootstrap credential. Startup rejects missing, malformed or equal enabled credentials. The bridge credential is stored with Windows current-user DPAPI in `host-bridge.dpapi` with literal `127.0.0.1` origin, instance ID and an eight-hour companion expiry. The launcher holds an exclusive runtime lock, removes stale registration before startup and removes its owned registration on normal shutdown.

Internal endpoints require exact Host and local port, a loopback peer, no Origin, the unexpired bridge token, POST, no query and no request body. A bridge credential cannot use authenticated UI approval routes. A UI bootstrap token cannot command internal routes.

The model-facing command path never creates consent. The local browser UI separately grants sharing consent, then a second submission approval after the package is filled. The latter is bound to application, package hash, recipient origin, profile version, resume hash and UI session; it expires after ten minutes. Approval alone sends zero submission POSTs.

`application_execute_approved` can consume only that stored approval. The SQLite journal changes `AwaitingSubmissionApproval` to `Submitting` while consuming it in one conditional update. Concurrent or repeated execution returns the terminal record and does not send a second POST. Restart recovery clears an unconsumed submission approval before new sharing; an interrupted claimed attempt becomes `SubmittedUnverified`.

## Client fail-closed behavior

The MCP bridge client uses a fresh HTTP/1.1 connection with proxy, cookies, redirect and automatic workflow retry disabled. A 3xx response is rejected, and a two-Kestrel regression proves that the redirect sink receives neither a request nor the bridge token. Responses are streamed with an 8 KiB cap.

Impossible success summaries are rejected: `SubmittedVerified` requires a nonblank bounded receipt; other states cannot carry a receipt; and `SubmissionApproved` is valid only while awaiting submission approval. A definite pre-claim `ConsentRequired` remains a policy denial. HTTP 500, ambiguous concurrency and transport/cancellation failures during execute become `CommandOutcomeUnknownCheckStatus`. The caller reads `application_get_status`; it does not retry execution.

## TDD and review evidence

The independent review added failing regressions before the fixes:

| Evidence | Initial result | Defect demonstrated |
|---|---:|---|
| `artifacts/verification/w09-review-red/boundary-red.trx` | 1 passed, 2 failed | equal UI/bridge credentials were accepted; a query input reached the command instead of being rejected |
| `artifacts/verification/w09-review-red/response-red.trx` | 1 passed, 7 failed | impossible success summaries were accepted; 500 and ambiguous concurrency were classified as definite rejection rather than unknown outcome |
| `artifacts/verification/w09-review-red/ui-red.trx` | 0 passed, 1 failed | the already-open UI did not discover a draft created through the external STDIO host without reload |

The UI RED was the product behavior under test: missing external-draft refresh. Earlier intermediate harness failures used a GET-inclusive request counter when the invariant was zero **submission POSTs**, and one assertion referenced the wrong fixture receipt property. Those were test-harness mistakes, were corrected separately, and are not presented as product defects.

After the fixes, the combined focused run recorded:

```text
artifacts/verification/w09-review-green/bridge-ui-green.trx
Passed: 20, Failed: 0, Skipped: 0, Total: 20
```

The 20 cases comprise six coordinator cases, eight response cases, three boundary cases, one redirect case, one full browser UI plus actual STDIO-client workflow, and the existing happy path. The integrated workflow verified:

- all six tools and their read-only/destructive/closed-world annotations;
- profile confirmation is required before host draft creation;
- a host-created draft appears in the already-open UI;
- review preparation creates no approval;
- an approval-shaped unknown argument is rejected;
- sharing stops at `AwaitingSubmissionApproval` with zero submission POSTs;
- premature execute returns `ConsentRequired` without state mutation;
- UI submission approval alone still produces zero submission POSTs;
- execute plus repeated execute produce exactly one POST and one receipt;
- UI receipt, MCP execute response and read-only status agree on `SubmittedVerified`.

The CLI Release build completed with zero warnings. The launcher regression first exited 1, then passed with exit 0 after correction; its green path verifies dashboard assets, paired fixture health, the exclusive runtime lock and removal of a stale bridge registration on default-off startup.

The subsequent repository-wide `scripts/verify.ps1` run exited 0 and recorded **188 passed, 0 failed, 0 skipped** with zero build warnings under `artifacts/verification/20260919T215755Z`: Core 58, Document 15, E2E 59, Infrastructure 32, MCP 14 and Workspace 10. Launcher, the initial 11-case and expanded 300-case fixture evaluations, and repository scans also passed. The earlier public/clean-checkout/CI count remains 137 for its own exact commit; this later local result does not rewrite that history.

## Verification boundary

The 20-case workflow uses the real dashboard, Playwright UI, fake career server, DPAPI registration, production bridge client and official C# STDIO MCP client. It does not use a language model. Separately, official Codex CLI 0.155.0 with existing ChatGPT authentication successfully called the read-only capability tool as recorded in `docs/evidence/codex-host.md`.

The prepared real-Codex multi-turn synthetic workflow harness is not claimed as run here. These results do not implement or verify the master plan's general 12-tool service, live employers, remote MCP, paid APIs or LinkedIn automation.
