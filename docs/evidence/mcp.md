# W09 initial STDIO MCP evidence

Date: 2026-09-18
SDK: .NET 10.0.401, `ModelContextProtocol` 2.2.0, `Microsoft.Extensions.Hosting` 10.0.12
Mode: local Fixture; synthetic-only; no paid/model/browser/platform call

## Implemented slice

`JobAgent.Mcp` is a real hosted STDIO MCP subprocess. Its runtime directory comes from `JOBAGENT_RUNTIME_DIR`, defaulting to `%LOCALAPPDATA%\JobApplicationAgent\demo`. The tool arguments accept only canonical GUID references; no path, URL, shell, approval, browser, or submission argument exists.

The server exposes these read-only, closed-world tools:

| Tool | Tested output boundary |
|---|---|
| `runtime_get_capabilities` | Fixture, synthetic-only, LinkedIn blocked, paid API disabled, approval minting disabled, host `NotVerifiedOnHost`, and the exact read-only tool list. |
| `profile_get_summary` | Reads an existing DPAPI-protected profile through `ProfileRepository`; returns reference, version, and current verified professional skill names only. |
| `application_get_status` | Reads an existing `ApplicationJournal` record; returns reference, state, receipt/application evidence identifiers and verification time only. |

The profile response type has no name, email, salary, private minimum, raw source text, or source document fields. The application response has no answers, resume hash/bytes, contact fields, recipient, or approval receipt payload.

## TDD evidence

Raw logs are ignored under `artifacts/mcp/`.

| Phase | Command | Exit | Evidence |
|---|---|---:|---|
| RED contract | `dotnet test tests/JobAgent.Mcp.Tests/JobAgent.Mcp.Tests.csproj --no-restore --nologo` | 1 | 1 failed assertion: `ReadOnlyTools` absent (`contract-red.log`). |
| GREEN contract | same command after minimal contracts | 0 | 1 passed (`contract-green.log`). |
| RED behavior | same command with storage/protocol behavior tests | 1 | 4 failed, 7 passed. Verified skills and application evidence were missing; the initial subprocess path was invalid (`behavior-red.log`). |
| RED protocol | filtered `StdioProtocolTests` after correcting the harness path | 1 | Real official-client calls showed unknown arguments were accepted and the capability assertions continued to the response checks (`stdio-red.log`). |
| GREEN behavior/protocol | full MCP test project | 0 | 11 passed, 0 failed (`behavior-green.log`). |
| Final suite after runtime filename/setup corrections | full MCP test project | 0 | 12 passed, 0 failed (`final-tests.log`). |
| Locked restore + build | `dotnet restore src/JobAgent.Mcp/JobAgent.Mcp.csproj --locked-mode`; `dotnet build ... --no-restore` | 0 | Restore succeeded; build completed with 0 warnings and 0 errors (`final-restore.log`, `final-build.log`). |

The passing protocol tests use the official `StdioClientTransport` with environment inheritance disabled and a small explicit allowlist. They launch the compiled MCP DLL as a child process, negotiate protocol `2025-11-25` through the real initialize handshake, list all tools, invoke all three, reject an unknown `path` field, verify read-only/closed-world annotations, and confirm private salary/contact data is absent.

Successful parsing by the official client is the `StdoutContainsOnlyProtocol` check: application logs are configured for STDERR, while STDOUT is consumed exclusively by the SDK transport. Server STDERR is also checked for the synthetic contact email.

## Local CLI observation

`codex --version` returned `codex-cli 0.44.0`, exit 0. `codex mcp add --help` returned exit 0 and reported the experimental `codex mcp add [OPTIONS] <NAME> [COMMAND]...` interface with `--env <KEY=VALUE>`. No add/remove/configuration command was run.

## Status and limitations

**W09 is a tested partial slice.** Real STDIO protocol behavior is verified with an official client subprocess. Real Codex host use is `NotVerifiedOnHost`; no model used the tools. The remaining master-plan tools, host-mediated synthetic end-to-end run, write proposals, review preparation, approved execution, cancellation, real host setup smoke, and remote transports are not implemented or claimed.

The application journal is currently synthetic-only. Profile reads are Windows DPAPI current-user reads. Missing databases are not created by MCP. The server does not instantiate `DemoWorkflow`, recover interrupted browser work, or acquire an active browser session.
