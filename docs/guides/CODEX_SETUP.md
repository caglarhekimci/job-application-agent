# Local Codex STDIO MCP setup

This guide describes the initial W09 read-only fixture server. It exposes exactly three tools:

- `runtime_get_capabilities`
- `profile_get_summary`
- `application_get_status`

It cannot mint approval, prepare or execute an application, control a browser, read an arbitrary path, fetch a URL, or run a shell command. LinkedIn remains blocked. The server does not call a model or paid API.

## Build and paths

Use the repository-pinned .NET 10.0.401 toolchain in PowerShell:

```powershell
. ./scripts/use-toolchain.ps1
dotnet restore ./src/JobAgent.Mcp/JobAgent.Mcp.csproj --locked-mode
dotnet build ./src/JobAgent.Mcp/JobAgent.Mcp.csproj --no-restore
```

On the verified Windows environment, the executable paths are:

```text
%LOCALAPPDATA%\JobApplicationAgent\tools\dotnet\dotnet.exe
<repository>\src\JobAgent.Mcp\bin\Debug\net10.0\JobAgent.Mcp.dll
```

The runtime directory is process configuration, never a tool argument. Set `JOBAGENT_RUNTIME_DIR` to the existing local runtime directory. If omitted, the server reads `%LOCALAPPDATA%\JobApplicationAgent\demo`. It expects existing `profiles.db` and `synthetic-applications.db` files and does not silently initialize them when a record is missing.

## Inspect before configuring Codex

The original global CLI was `codex-cli 0.44.0`. An isolated official CLI 0.155.0
was subsequently used for an actual ChatGPT-authenticated capability smoke test.
See [the host evidence](../evidence/codex-host.md) and the opt-in
`scripts/test-codex-host.ps1` for the verified per-process configuration; it does
not modify the user's global configuration. The legacy CLI help below is historical:

```text
codex mcp add [OPTIONS] <NAME> [COMMAND]...
--env <KEY=VALUE>
```

Review the concrete command without running it:

```powershell
$dotnet = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\tools\dotnet\dotnet.exe'
$dll = Resolve-Path '.\src\JobAgent.Mcp\bin\Debug\net10.0\JobAgent.Mcp.dll'
$runtime = Join-Path $env:LOCALAPPDATA 'JobApplicationAgent\demo'
"codex mcp add --env JOBAGENT_RUNTIME_DIR=$runtime job-application-agent $dotnet $dll"
```

Running `codex mcp add` changes the user's global Codex configuration. This repository does not run that command automatically. If the user chooses to configure it, the CLI 0.44.0 help indicates the corresponding command shape is:

```powershell
codex mcp add --env "JOBAGENT_RUNTIME_DIR=$runtime" job-application-agent $dotnet $dll
```

Use `codex mcp get job-application-agent` and `codex mcp list` to inspect a user-created entry. The protocol integration test in this repository launches the same DLL through the official C# `StdioClientTransport`, performs a real initialize handshake, lists the tools, and invokes each one.

## Verification boundary

The server uses the official `ModelContextProtocol` 2.2.0 package and the STDIO transport documented by the [official C# SDK](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md). STDOUT is reserved for protocol traffic and host logs go to STDERR.

The automated subprocess test is protocol/client verification. Separately, on
2026-09-19, Codex CLI 0.155.0 with gpt-6-astra and existing ChatGPT authentication
invoked `runtime_get_capabilities` successfully. No global MCP entry was added.
The server's generic `HostExecution=NotVerifiedOnHost` field remains conservative;
the observed smoke proves one tool on one host, not the full application workflow.

This W09 slice is Windows-first because existing profiles are protected with Windows DPAPI. It reads the synthetic local runtime only. Real candidate data, live applications, remote MCP, browser actions, and submission are outside this slice.
