# Actual Codex host verification: local twelve-tool workspace

Date: 2026-09-20. **Status: preparation and actual two-turn Codex host run passed.**
This is one synthetic local-workspace integration demonstration. Existing actual-host evidence for
the separate synthetic six-tool mode remains in [codex-workflow.md](codex-workflow.md).

The harness is `scripts/test-codex-local-service.ps1`; its trusted synthetic UI
fixture is `scripts/local-host-fixture/Program.cs`. It consumes already-built,
stable MCP and companion binary directories. Only the temporary helper project is
compiled, outside the checkout, with empty NuGet package sources. It does not
rebuild application source or change the user's installed configuration.

```powershell
./scripts/test-codex-local-service.ps1 -PrepareOnly -McpDirectory <stable-mcp-directory> -CompanionDirectory <stable-companion-directory>
```

Preparation starts an isolated loopback companion in local-only mode and a trap
site. A helper simulates the trusted local UI login/import/profile/job review with
synthetic data. No application, data-sharing approval or submission approval is
created. It then performs a direct STDIO handshake, checks the exact twelve-tool
catalog and calls capabilities against the same protected workspace. This path
does not copy authentication or call a model.

After the integrated application passed its 255-test release suite, the explicit
`-RunLive` path uses the installed official native Codex CLI 0.155.0 and the
existing ChatGPT subscription login. A temporary `CODEX_HOME` with an ACL for the
current user holds only a copied existing login and minimal test configuration.
The harness verifies ChatGPT authentication, removes API-key environment variables,
and provides no paid API fallback, installation, login, billing or purchase path.

The executed call profile was two bounded model turns, exactly twelve distinct tool
calls in total:

1. Discover capabilities/current references; inspect the safe profile summary;
   propose a pending profile change; import synthetic job text without fetching
   its URL; evaluate that unreviewed job; create a draft using the original reviewed
   job/profile/resume references; retrieve its persisted unresolved questions.
2. Propose an evidence-grounded answer at the observed revision; request local
   review; actually attempt execution once and observe `BlockedPermission`; cancel
   the application; read its final status.

All tools are restricted to the local twelve-tool allowlist. Shell, browser,
computer-use, apps, plugins, image tools, hooks and delegation are disabled, and
unexpected action events fail verification. The read-only sandbox is retained.
Only the second isolated process grants the host permission to invoke the named
`application_execute_approved` and `application_cancel` tools, using the same
version-pinned per-tool setting established in the earlier synthetic host test.
This records authorization to run the synthetic test; it does not mint an
application approval or remove the application's live-adapter permission denial.

Success requires actual completed MCP events and their structured results, not
the model's narrative. The separate fixture checks the stored result: one cancelled
application, one pending profile proposal, one pending job proposal and one pending
answer proposal; the reviewed profile/current job and answer memory remain
unchanged; no approval or receipt exists; the trap site's request/submission counts
remain zero. Private synthetic salary/contact values must not appear in host tool
results. An expected policy denial is recorded distinctly from a successful call.

Every attempt retains ignored process exit records, JSONL/stdout/stderr evidence,
binary hashes, available actual usage counters and a pass/failure summary under
`artifacts/codex-local-service/`. Failures are not automatically retried. The
temporary runtime/login copy is deleted only after checking its resolved owned
path; original user configuration and input binary hashes must remain unchanged.
Unavailable usage is recorded as unavailable, not zero. No model quality,
real-employer permission, independent adoption or store acceptance is inferred.

## Actual preparation result

`-PrepareOnly` completed with **exit 0** at 2026-09-19 23:04 UTC (2026-09-20 local
date), using a stable copy of the current Debug MCP and E2E companion outputs in
`artifacts/local-host-inputs/20260919T230343981Z/`.
Evidence: `artifacts/codex-local-service/20260919T230353393Z/`.

The isolated helper compiled with **0 warnings and 0 errors**. The authenticated
synthetic UI imported the synthetic TXT, confirmed its profile and reviewed its
job. Direct STDIO discovery returned exactly the twelve expected tools;
`runtime_get_capabilities` returned `HostMediated`, `CompanionAvailable=true`,
`CanMintApproval=false`, `PaidApiEnabled=false` and the same protected workspace
references. The fixture had zero applications and zero trap requests.

The recorded summary reports `ActualModelTurns=0`, authentication `NotAccessed`,
unchanged original configuration and input binaries, and successful removal of the
temporary private directory. The PowerShell parser check also passed; its initial
reserved-keyword helper-name error was corrected before this preparation run.
The preparation preceded the separately authorized two-turn model run below.

## Actual model-host result

The first live attempt passed with **exit 0**, from 2026-09-19 23:09:28 through
23:10:52 UTC. No failed model attempt or automatic retry occurred. The input was a
fresh stable copy of the fully tested Release outputs:
`artifacts/local-host-inputs/20260919T230918870Z/`.
Evidence: `artifacts/codex-local-service/20260919T230928394Z/`.

Model: `gpt-6-astra`; native CLI: `0.155.0`; authentication: existing ChatGPT
subscription login copied into the isolated temporary test directory. The actual
events contain all twelve distinct MCP calls, exactly once each: **11 completed
results and 1 expected policy denial**. The denied call is an actual
`application_execute_approved` invocation with MCP error `-32600: BlockedPermission`,
not a refusal inferred from the model's final text. Its destructive annotation was
retained; the process-only host invocation setting did not bypass application policy.

| Actual turn | MCP calls | Input tokens | Cached input tokens | Output tokens | Reasoning output tokens |
|---|---:|---:|---:|---:|---:|
| Discover and propose | 7 | 116,561 | 100,608 | 826 | 36 |
| Propose, deny, cancel | 5 | 32,853 | 28,288 | 447 | 0 |
| Total | 12 | 149,414 | 128,896 | 1,273 | 36 |

These are the CLI's reported counters; cached input is part of the reported input,
not an additional call or invented monetary cost. No paid API or credit purchase
was used.

The independent trusted fixture confirmed the stored application
`8de1bc15-2084-46be-8e58-b5eb437efe38` ended **Cancelled**, workspace revision 9.
There was one pending profile proposal, one pending imported job and one pending
answer proposal. The reviewed profile remained at version 3, the original reviewed
job was unchanged, and answer memory still contained zero entries. No application
approval, submission or receipt existed. The trap site's entire request count was
**0**, including **0 submission POSTs**. Private synthetic contact/salary values
were absent from inspected model tool outputs.

The helper compiled with zero warnings/errors. The final summary confirms unchanged
original Codex configuration and input binary hashes, and removal of the private
test directory/login copy. The MCP DLL SHA-256 was:

```text
ac97e1fe04ad3c59ed1156e2a21c9801e5492950f80dad283bb76914f4a41bb4
```

This demonstrates actual model use of the twelve-tool contract and its pending-data,
permission-denial and cancellation behavior. It does not demonstrate a permitted
live employer submission or assess open-ended answer quality. The capability
response's static `HostExecution=NotVerifiedOnHost` remains conservative at runtime;
this dated evidence records the verified build and scenario rather than changing
all installations' runtime claim.
