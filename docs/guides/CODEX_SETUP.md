# Local Codex STDIO MCP setup

The Windows fixture MCP uses the official C# `ModelContextProtocol` 2.2.0 SDK and STDIO transport. It makes no paid API call. STDOUT is reserved for MCP protocol messages; diagnostics go to STDERR.

## Default: three read-only tools

Without extra configuration the server exposes exactly:

- `runtime_get_capabilities`
- `profile_get_summary`
- `application_get_status`

These tools accept fixed GUID references. They cannot mint approval, control the browser, accept a path or URL, run a shell command, or submit an application. LinkedIn remains blocked.

Use the repository-pinned .NET 10.0.401 toolchain:

```powershell
. ./scripts/use-toolchain.ps1
dotnet restore ./src/JobAgent.Mcp/JobAgent.Mcp.csproj --locked-mode
dotnet build ./src/JobAgent.Mcp/JobAgent.Mcp.csproj --no-restore
```

The process must share the companion's runtime directory. If `JOBAGENT_RUNTIME_DIR` is omitted, both use `%LOCALAPPDATA%\JobApplicationAgent\demo`.

With official Codex CLI 0.155.0, the persistent command shape is:

```powershell
$dotnet = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\tools\dotnet\dotnet.exe'
$dll = (Resolve-Path '.\src\JobAgent.Mcp\bin\Debug\net10.0\JobAgent.Mcp.dll').Path
$runtime = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\demo'

codex mcp add --env "JOBAGENT_RUNTIME_DIR=$runtime" job-application-agent -- $dotnet $dll
```

`codex mcp add` changes the user's Codex configuration, so the repository does not run it automatically. Inspect a user-created entry with `codex mcp get job-application-agent` or `codex mcp list`.

## Explicit synthetic-command opt-in

Set `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1` in **both** the companion process and the MCP process to add exactly three commands:

- `application_create_draft`
- `application_prepare_review`
- `application_execute_approved`

For the companion:

```powershell
$env:JOBAGENT_RUNTIME_DIR = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\demo'
$env:JOBAGENT_ENABLE_SYNTHETIC_COMMANDS = '1'
./scripts/run-demo.ps1
```

For a Codex MCP entry, add the same opt-in and runtime directory:

```powershell
codex mcp add `
  --env "JOBAGENT_RUNTIME_DIR=$runtime" `
  --env "JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1" `
  job-application-agent -- $dotnet $dll
```

If either process lacks the opt-in, the command workflow is unavailable. The companion creates a current-user DPAPI-protected loopback registration only while enabled. It contains a separate 256-bit bridge token, a literal `127.0.0.1` origin and an eight-hour default expiry (the store rejects lifetimes over twelve hours). Normal shutdown removes the owned registration; the exclusive launcher also removes a crashed instance's stale registration before startup.

## Explicit personal-workspace opt-in (12 tools)

The general local service reads the same protected personal workspace shown in
the dashboard. To enable it, set `JOBAGENT_ENABLE_LOCAL_COMMANDS=1` in both the
companion and MCP process, and leave `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=0`.
The two command modes cannot be enabled together. The packaged inspector always
forces both flags off; use a source or app-package MCP DLL for this opt-in.

```powershell
$env:JOBAGENT_ENABLE_SYNTHETIC_COMMANDS = '0'
$env:JOBAGENT_ENABLE_LOCAL_COMMANDS = '1'
./scripts/run-demo.ps1
```

With the `$dotnet`, `$dll` and `$runtime` paths defined above, a separate MCP entry is:

```powershell
codex mcp add `
  --env "JOBAGENT_RUNTIME_DIR=$runtime" `
  --env "JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=0" `
  --env "JOBAGENT_ENABLE_LOCAL_COMMANDS=1" `
  job-application-agent-local -- $dotnet $dll
```

First import and review a synthetic CV/job in the local UI when trying this mode.
Capabilities return opaque current references; tools never accept local file paths.
The 12 tools cover capability/profile summaries, pending profile patches, pasted
job proposals/evaluation, draft creation, missing questions, pending answers,
review requests, status, cancellation and execution checks. Profile/job/answer
proposals require local user review. The personal workspace has no authorized live
adapter, so execution returns `BlockedPermission` and never visits an employer.
The separate synthetic mode above retains the tested browser submission path.

Neither mode makes a paid model API call. Actual conversation use consumes your
normal Codex allowance. The program cannot enforce the host's own model usage or
control independent browser tools supplied by that host.

## Synthetic approval and uncertain outcomes

The command tools can create the fixed synthetic draft, request local UI review and execute an approval already stored by the companion. They do not accept a resume, answer, contact detail, URL, path, browser instruction, session value, approval flag or approval receipt.

The user still performs two independent actions in the authenticated local UI:

1. approve sharing the fixed synthetic package so the managed browser may fill it;
2. approve submission after reviewing the filled package.

Submission approval is bound to the application ID, payload hash, recipient origin, profile version, resume hash and UI session. It expires within ten minutes (earlier when a relevant answer expires), is rechecked at outbound transmission, and is consumed by one durable claim. Merely calling `application_prepare_review` does not approve either action.

The bridge client uses HTTP/1.1 directly to literal loopback with proxies, cookies and redirects disabled. It never follows a redirect or forwards the bridge token to another origin. It does not retry execution. If execution returns `CommandOutcomeUnknownCheckStatus`, use the read-only `application_get_status` tool to poll the durable journal. Do not call `application_execute_approved` again to guess the outcome.

## Verified boundary

The automated protocol tests launch the real MCP DLL through the official C# STDIO client, perform the initialize handshake, list tools and invoke them. A focused W09 integration run passed 20/20 cases, including the actual browser UI plus a real STDIO client, denial without UI approval, one POST after approval, repeat execution remaining at one POST, redirect blocking and uncertain-response handling. See [W09 bridge evidence](../evidence/w09-bridge.md).

An isolated official Codex CLI 0.155.0 previously invoked the capability tool with
existing ChatGPT authentication. That single host smoke and the deterministic
UI/STDIO integration tests do not claim a completed live-site agent. The default
inspector and fixed synthetic workflow retain their narrow scope. The separate
12-tool personal service is covered by [local-service evidence](../evidence/w09-local-service.md);
it shares protected UI records, and still does not automate employer sites or LinkedIn.

The separate [three-turn Codex workflow](../evidence/codex-workflow.md) also passed:
the model prepared a draft, received `ConsentRequired` before UI approval, then
executed the approved synthetic application and read the matching receipt. The
test driver simulated protected UI approval; it was not a real job application.
Codex may additionally ask permission to call a destructive MCP tool. The isolated
test granted only the synthetic execute tool at that host layer; application
consent remained required. Keep ordinary host confirmations enabled when using
the companion interactively; do not treat chat permission as UI package approval.

## Extended synthetic form and missing questions

For the optional select/checkbox/radio/conditional-field fixture, use a separate
local runtime so an already completed demo application is not reused:

```powershell
$env:JOBAGENT_RUNTIME_DIR = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\extended-demo'
$env:JOBAGENT_ENABLE_LOCAL_COMMANDS = '0'
$env:JOBAGENT_ENABLE_SYNTHETIC_COMMANDS = '0'
$env:JOBAGENT_EXTENDED_FORM = '1'
./scripts/run-demo.ps1
```

Review the missing preference answers in the UI, then separately approve sharing
and final submission. Choosing phone adds a call-window question; the fixture
does not infer or request a phone number. A changed or expired answer requires
fresh review and approval. This mode uses only the local synthetic career site.
