# W09 initial real STDIO slice

Create a real .NET10 STDIO MCP server with official ModelContextProtocol 2.2.0
and Microsoft.Extensions.Hosting (central versions already pinned).
Own src/JobAgent.Mcp, tests/JobAgent.Mcp.Tests, docs/guides/CODEX_SETUP.md,
docs/evidence/mcp.md only. Do not edit shared solution, status docs, Core,
Infrastructure or Web. Parent will add project to solution. No subagents/commits.

Read master sections 11, W09 and AGENTS. No approval, browser, generic URL/file/shell
tools. Provide only these actual initial read-only tools (clearly W09 partial):
- runtime_get_capabilities: Fixture, synthetic-only, LinkedIn blocked, no paid API,
  no approval minting, real host NotVerifiedOnHost, supported read-only tools.
- profile_get_summary(profileRef): read actual confirmed profile via ProfileRepository
  from configured local runtime dir; validate fixed GUID/ref; output minimal verified
  skills/version, no contact, target salary or private minimum, no raw source text.
- application_get_status(applicationRef): read existing ApplicationJournal by Guid;
  output state/evidence/reference only, no answers, resume bytes or contact fields.

Do not instantiate DemoWorkflow or call recovery from MCP: another process may own
active browser. ProfileRepository read is safe after existing db check; don't create
new tables/file silently just to return no data. Runtime directory comes from process
configuration, not tool arguments; default %LOCALAPPDATA%/JobApplicationAgent/demo.
No arbitrary paths accepted from tool calls. Use stderr logs and stdout protocol only.
Test invalid GUID/unknown tool fields, no approval tool, private minimum absence,
and actual STDIO handshake/list/call with real client/process. TDD red assertions
then green, raw logs artifacts/mcp/. Use pinned .NET via scripts/use-toolchain.ps1.

You may browse official MCP C# docs for API shapes; must verify latest APIs instead
of guessing. Do not configure user's Codex account/config. Provide accurate setup
guide with explicit dotnet executable path, DLL path and local CLI help observations
(installed CLI 0.44.0). Real Codex host execution remains unverified unless actually
run; don't treat protocol client as host smoke. No paid/model calls.

Report actual protocols tested, commands, exit codes, counts in docs/evidence/mcp.md.
No broad capabilities or submit support claims; explain remaining W09 tools/integration.
