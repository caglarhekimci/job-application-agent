# W09 actual Codex host capability smoke

Verified on 2026-09-19 using Windows, official isolated `codex-cli 0.155.0`,
`gpt-6-astra`, existing ChatGPT authentication, and the compiled Release MCP server.
The user explicitly authorized normal Codex allowance for the required tests.
No API key, credit purchase, paid API service, account upgrade, global Codex config
change, browser action, personal profile read, or application submission was used.

## Observed result

The actual Codex JSONL event stream contains one started and one completed
`mcp_tool_call` for server `job_agent`, tool `runtime_get_capabilities`, arguments
`{}`, status `completed`, and a structured result:

```json
{
  "mode": "Fixture",
  "syntheticOnly": true,
  "linkedIn": "Blocked",
  "paidApiEnabled": false,
  "canMintApproval": false,
  "hostExecution": "NotVerifiedOnHost"
}
```

There were no other action events. The empty temporary runtime was still empty
after the saved harness run. Codex exited 0 and the harness exited 0.
The `hostExecution` field is a conservative server constant; this run does not
rewrite it or establish a guarantee for other hosts. The narrow observed status
is **VerifiedOnHostCapabilitySmoke**, not complete W09 or a host-driven application.

## Reproduction and evidence validation

Build the Release MCP project using the pinned toolchain first. The harness does
not build or install dependencies. The default executable is the per-user isolated
0.155.0 installation; `-CodexPath` accepts another official native executable.

```powershell
# Opt-in: uses the existing ChatGPT plan's normal Codex allowance.
pwsh -NoProfile -File scripts/test-codex-host.ps1 -RunLive

# Offline replay: no model, network or MCP call.
pwsh -NoProfile -File scripts/test-codex-host.ps1 -EvidenceLog <events.jsonl>
```

The live script requires ChatGPT login, forces `forced_login_method="chatgpt"`,
removes inherited API-key environment variables from its child process, and uses
per-invocation configuration. It supplies an empty, unique runtime outside the
repository. `required=true` waits for MCP initialization; the tool allowlist exposes
only the capability tool. Shell, app connectors, plugins, additional agents, shell
snapshots and web search are disabled for this invocation. A bounded timeout kills
the owned process tree without automatic model retries. The JSONL validator checks
the real tool event, constrained values, single completed turn, and absence of
other action events; the model's prose alone cannot pass.

| Check | Exit | Actual evidence |
|---|---:|---|
| Replay of earlier unavailable-tool turn | 1, expected | `artifacts/logs/codex-host-smoke-current.jsonl`; no MCP call event |
| Initial actual host invocation with required MCP | 0 | `artifacts/logs/codex-host-smoke-required.jsonl`; one actual MCP call |
| Offline validation of that invocation | 0 | SHA256 `328b0f5a2a1b9ce3fe534b4c5eb0a8d634196c3946102459700ff79e23be6f0a` |
| Saved harness live run at 20:43:15 UTC | 0 | `artifacts/codex-host/20260919T204315101Z/events.jsonl`, `stderr.log`, `summary.json` |
| Whitespace check for harness | 0 | `git diff --check -- scripts/test-codex-host.ps1` |

The saved harness event log SHA256 is
`63805fed1da6689ba0f88355fe339fa47f2fec13841fd444f39485e53c3be963`.
Raw logs remain ignored local artifacts; this document contains only capability
values, version information and reported usage.

## Actual reported model usage

These are Codex event counters, not a price calculation or an account balance.
Cached input is reported separately and is not added to total input.

| Run | Input | Cached input | Output | Reasoning output |
|---|---:|---:|---:|---:|
| Earlier unavailable-tool turn (before this diagnostic) | 18,464 | 11,776 | 141 | 120 |
| Initial successful diagnostic | 25,359 | 12,416 | 100 | 0 |
| Saved harness verification | 20,457 | 18,304 | 134 | 57 |

## Diagnosis and scope

The original current-CLI attempt returned only an unavailable-tool message. A
read-only `codex mcp get job_agent --json` confirmed that a dotted override parsed
the command and DLL path correctly. The successful invocation used that override,
a required server, and a narrow allowlist. Optional MCP servers have a documented
default initial-catalog grace of 1,000 ms, so startup timing is a plausible cause of
the earlier omission. It was not isolated with a separate timing experiment and
is not represented as a proven root cause.

The installed global CLI 0.44.0 remains unchanged. Its earlier HTTP 400 for
`gpt-6-astra` is separate from this successful 0.155.0 test. Current 0.155.0 help
uses `codex mcp add <name> -- <command> ...`; no persistent `mcp add` command was run.
The existing setup guide's old 0.44.0 observation is historical.

This verifies one real model/host read-only capability call. It does not measure
answer quality, verify all three tools through Codex, complete the broader MCP
write/proposal workflow, prove an approved host-mediated end-to-end application,
or validate a fresh Windows account installation. Those remain separate checks.

Official sources checked on 2026-09-19:

- [Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli): required startup and tool allowlists.
- [Non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode): ephemeral execution, per-run config isolation and JSONL events.
- [Configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference): forced ChatGPT authentication and optional MCP startup grace.
