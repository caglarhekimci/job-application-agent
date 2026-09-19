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

## Approval and uncertain outcomes

The command tools can create the fixed synthetic draft, request local UI review and execute an approval already stored by the companion. They do not accept a resume, answer, contact detail, URL, path, browser instruction, session value, approval flag or approval receipt.

The user still performs two independent actions in the authenticated local UI:

1. approve sharing the fixed synthetic package so the managed browser may fill it;
2. approve submission after reviewing the filled package.

Submission approval is bound to the application ID, payload hash, recipient origin, profile version, resume hash and UI session. It expires after ten minutes and is consumed by one durable claim. Merely calling `application_prepare_review` does not approve either action.

The bridge client uses HTTP/1.1 directly to literal loopback with proxies, cookies and redirects disabled. It never follows a redirect or forwards the bridge token to another origin. It does not retry execution. If execution returns `CommandOutcomeUnknownCheckStatus`, use the read-only `application_get_status` tool to poll the durable journal. Do not call `application_execute_approved` again to guess the outcome.

## Verified boundary

The automated protocol tests launch the real MCP DLL through the official C# STDIO client, perform the initialize handshake, list tools and invoke them. A focused W09 integration run passed 20/20 cases, including the actual browser UI plus a real STDIO client, denial without UI approval, one POST after approval, repeat execution remaining at one POST, redirect blocking and uncertain-response handling. See [W09 bridge evidence](../evidence/w09-bridge.md).

An isolated official Codex CLI 0.155.0 previously invoked the capability tool with existing ChatGPT authentication. That single host smoke and the deterministic UI/STDIO integration tests do not claim a completed live-site agent. This remains a Windows-first, fixed-synthetic workflow. It is not the master plan's general 12-tool service, does not operate on real candidate applications, and does not automate LinkedIn.

The separate [three-turn Codex workflow](../evidence/codex-workflow.md) also passed:
the model prepared a draft, received `ConsentRequired` before UI approval, then
executed the approved synthetic application and read the matching receipt. The
test driver simulated protected UI approval; it was not a real job application.
Codex may additionally ask permission to call a destructive MCP tool. The isolated
test granted only the synthetic execute tool at that host layer; application
consent remained required. Keep ordinary host confirmations enabled when using
the companion interactively; do not treat chat permission as UI package approval.
