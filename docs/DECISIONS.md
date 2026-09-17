# Decisions

## ADR-001 — Accepted architecture
The supplied master plan is the approved architecture. Keep .NET 10, C#, ASP.NET
Core, EF Core/SQLite, React/TypeScript, Playwright .NET and official C# MCP SDK.
Do not reopen architecture approval or generate a replacement plan.

## ADR-002 — Isolation and toolchain
The parent directory is not a git repository and contains private documents.
Create only job-application-agent/ on work/local-synthetic. No pre-existing source
was overwritten. A new repository already isolates the work; another worktree
would add no isolation. .NET SDK 10.0.401 is installed per-user from Microsoft's
official release metadata, with SHA512 validation. No administrator install or
global PATH modification. SDK and runtime versions are distinct.

## ADR-003 — Runtime and live targets
Fixture is the initial mode, zero paid API budget. The synthetic site is bound to
literal loopback; no general browsing endpoint. Native host browsers are outside
our managed runtime guarantees. LinkedIn permissions are independent of user consent.

## ADR-004 — Review and data
Store personal runtime state outside the repo. Windows DPAPI protects sensitive
payloads; standard SQLite is not claimed to be encrypted. Approval binds recipient,
application, profile version, resume bytes and answers; sharing and submission are
separate purposes. Uncertain post-submit outcomes cannot be blindly retried.

## ADR-005 — Delivery evidence
Use the master plan's W00-W18 as execution order. Implement the earliest end-to-end
slice and expand packages honestly; a partial package is not marked complete.
Persist test logs under ignored artifacts/ and publish only synthetic summaries.
