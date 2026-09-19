# Real Codex synthetic workflow harness

Verified 2026-09-19 UTC / 2026-09-20 Istanbul. **Passed: real Codex host through
the complete synthetic application flow.** Three `gpt-6-astra` turns in official
Codex CLI 0.155.0 made five actual MCP calls. Missing local approval was rejected
by the application; a separate simulated trusted UI approval then permitted one
real Chromium submission to the loopback fixture, followed by receipt verification.
No real employer application or real human approval is claimed.

Successful command, exit **0**:

```powershell
pwsh -NoProfile -File scripts/test-codex-workflow.ps1 -RunLive -McpDirectory artifacts/workflow-host-binaries/mcp -CompanionDirectory artifacts/workflow-host-binaries/cli
```

Actual evidence: `artifacts/codex-workflow/20260919T220144149Z/`.
MCP DLL SHA-256: `59d48a3e63bc4cdaea7fcc2aaf9b8b1cb66f5f1260311030e5baafaff6fc72c8`.
`input-binaries.json` records all input DLL hashes; none changed during the run.
The fixture helper compiled with 0 warnings/errors. The original Codex config
hash was unchanged, and the temporary credentials/runtime directory was removed.

The runner is `scripts/test-codex-workflow.ps1`, with the isolated fixture driver
in `scripts/workflow-host-fixture/Program.cs`. It accepts previously built MCP and
companion DLL directories and never rebuilds shared project sources. A tiny
fixture-only project is compiled outside the checkout with package sources
disabled. It uses production DashboardHost/FakeCareerHost classes on random
literal loopback ports and the production DPAPI registration store. This tests
the host bridge, not the separate CLI launcher.

```powershell
pwsh -NoProfile -File scripts/test-codex-workflow.ps1 -RunLive -PrepareOnly -McpDirectory <built-mcp> -CompanionDirectory <built-cli>
pwsh -NoProfile -File scripts/test-codex-workflow.ps1 -RunLive -McpDirectory <built-mcp> -CompanionDirectory <built-cli>
```

`-PrepareOnly` uses no model and does not copy a login. Full execution uses the
existing ChatGPT login in a user-only temporary CODEX_HOME and three bounded
Codex CLI turns. API-key variables are removed; no paid API, install, account
change, purchase or global config modification is performed. The browser uses
the existing Playwright cache. Credentials, UI cookies, CSRF values, bridge token
and private runtime files remain outside the repository and outside model input.

Observed sequence, with every effect checked independently:

1. The trusted test driver uses authenticated UI HTTP routes to import and
   confirm the bundled synthetic profile. No application or POST existed yet.
2. The actual model calls `application_create_draft`, then
   `application_prepare_review` for the returned GUID. No approval was minted.
3. The trusted test driver simulates sharing approval through `/api/approve-share`.
   Production Playwright fills the actual synthetic form and uploads the CV,
   stopping at `AwaitingSubmissionApproval` with zero submission POSTs.
4. A second model turn calls `application_execute_approved`. `ConsentRequired`
   denied it with MCP error -32600; UI state and FakeCareer receipt counters
   remained unchanged at zero submissions and zero receipts.
5. The test driver simulates local submission approval through the protected
   `/api/approve-host-submit/{Guid}` UI route. It stored approval without sending
   the application: submission count was still zero.
6. The third model turn executes the existing approval and checks status. Both
   tools agreed on `SubmittedVerified` and the receipt. Independent server
   counters reported exactly one POST and one receipt. The uploaded filename was
   `synthetic-resume.txt`, salary was `100000`, and professional years were `3`.
   The uploaded CV hash matched the approved draft and bundled resume bytes.

Application reference: `bbeb5563-14de-4cdb-a5dc-c1408bb7a736`.
Receipt: `SYN-7e60c50a3db24cd2a8d96749db1ce9b5`.
Verified time returned by the status tool: `2026-09-19T22:02:34.8764498+00:00`.
CV SHA-256: `9D5794BE12E0B098980172BA792474BB0C1B7D827EDE4C3D05AC4D2A59D62F28`.

| Actual model turn | MCP calls | Input tokens | Cached input | Output tokens |
|---|---:|---:|---:|---:|
| Create draft and request review | 2 | 40,239 | 36,992 | 209 |
| Execute without application approval; expected rejection | 1 | 19,470 | 9,472 | 192 |
| Execute stored approval and verify status | 2 | 40,657 | 34,944 | 240 |
| Successful run total | **5** | **100,366** | **81,408** | **641** |

Reported reasoning output was 0 in all three turns. These are actual host usage
counters, not a dollar estimate or broad model-quality benchmark. The final
model answer matched the actual state/receipt and explicitly identified a local
synthetic test with no real employer application.

These are **simulated trusted UI approvals in a synthetic test**, not the user
approving a real job application. Only four explicitly allowed workflow MCP
tools are exposed to the model; shell, browser, plugins and other tools are
disabled or rejected by event validation. The host cannot invoke a UI approval
tool. No employer or LinkedIn received data.

The evidence directory contains actual JSONL events, sanitized UI assertions,
binary hashes, usage counters and a completion summary. A success summary is
emitted only after process cleanup and the original config hash check. Unexpected
actions, tool failures, uncertain submission or changed input binaries fail the
run; they are never automatically retried or marked passed.

## Scoped host permission and earlier failed run

The host's permission to invoke a tool and the application's stored UI approval
are separate gates. Destructive tool metadata remains **true**. The test uses
the supported per-tool host setting only for `application_execute_approved`,
only in the second and third isolated Codex processes:

```toml
[mcp_servers.job_agent.tools.application_execute_approved]
approval_mode = "approve"
```

This process-only setting records the user's explicit authorization to run these
synthetic tests. It does not change global config, relax the read-only sandbox,
approve other tools, grant application consent or enable any external target.
The still-rejected pre-approval call proves the application's separate gate.
The setting is defined by the version-pinned official
[0.155.0 config schema](https://github.com/openai/codex/blob/rust-v0.155.0/codex-rs/core/config.schema.json)
and handled by the official
[MCP per-tool permission implementation](https://github.com/openai/codex/blob/rust-v0.155.0/codex-rs/codex-mcp/src/mcp/mod.rs#L81).
The harness uses `--strict-config`. No unrestricted mode or annotation change
was used. This test approval arrangement is not a recommended default for real
application workflows.

The initial `-PrepareOnly` run passed, exit 0, without a login copy or model call:
`artifacts/codex-workflow/20260919T215649685Z/`. It verified the actual synthetic
UI session/import/confirm and bridge registration with zero submissions.

The first live attempt, `artifacts/codex-workflow/20260919T215742939Z/`, exited
**1** after the second turn. Create/review and real browser fill succeeded, but
Codex rejected the destructive execute call before it reached the application:
`MCP tool call requires approval, but approval policy is never`. The model had
also supplied `application_id` instead of the schema's `applicationRef` key.
The model correctly said the host denial did **not** verify the application's
missing-approval check. No UI submission approval or POST occurred in that run.

After checking the pinned official source, the process-scoped setting above was
added; prompts also spell out the required `applicationRef` argument. The fresh
successful run used a new synthetic runtime and application. The failed attempt
is retained as failed evidence: its two turns reported **59,708 input**, **46,464
cached input**, **370 output**, **0 reasoning output** tokens. No automatic
retry or paid API was used. The complete task consumed those counters plus the
successful run's counters above, using the existing ChatGPT subscription login.

This is one bounded synthetic host integration demonstration, not evidence of
general job discovery, LinkedIn authorization, open-ended form handling, model
answer quality, independent adoption or public store approval.
