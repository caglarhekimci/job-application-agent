# Decisions

## ADR-010 — Fixed synthetic host commands and empirical model boundary
The default plugin keeps three read-only tools. Three additional MCP commands
are explicitly opt-in in both companion and host processes. They use a separate,
current-user DPAPI-protected loopback registration and cannot accept approval,
browser actions, paths, URLs or replacement packages. The companion UI alone
grants ten-minute package-bound approval; the durable journal claims one attempt.
Host-level tool permission is a second independent layer. Test-only scoped host
permission does not bypass the companion's UI consent.

The frozen actual-model pilot did not show B2 improvement (57/72 strict versus
B1 69/72). Proposed model wording remains unapproved, and deterministic rules
remain authoritative for the implemented submission path. Keep the original
pilot and the benchmark's annual-gross-policy limitation visible; do not tune
expectations after seeing output or advertise model-quality improvement.

## ADR-009 — Personal review workspace and cost constraint
The user authorizes required real Codex tests using the existing subscription's
included quota. No extra credit purchase or paid API is authorized. The local UI
adds a separate protected personal workspace with atomic document/profile/history
payloads and expected-revision updates. This initial single-row SQLite workspace
does not replace the existing profile repository migration chain or synthetic-only
application journal. It has no external target adapter. Plugin packaging remains
local; publisher verification/HTTPS or local-MCP approval is an external store gate.

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

## ADR-006 — Request transport boundary
Validate exact multipart answers/file bytes before the one permitted POST; fetch
with redirect/retry disabled. Durable claims and receipt checks do not guarantee
exactly-once delivery to arbitrary employers. Trust installed same-user code, not
page scripts. No OS-level firewall or control of native host tools is claimed.

## ADR-007 — Evidence and packaging scope
Small fixture evaluations use honest numerators/denominators and distinguish
scenario time from actual run time. Shared fixtures are not independent holdout.
Framework-dependent local packages omit browser caches, SDKs and personal data.
A clean checkout using current caches is not a fresh Windows user/OS validation.

## ADR-008 — Expanded authorization on 2026-09-19
The user now explicitly requests all master-plan work including public GitHub
push and Codex for OSS submission. Proceed toward those actions without asking
for the same generic permission again. Required unknown personal fields, actual
terms at submission time, platform permissions and independent adoption cannot be
invented. Continue local implementation while those external inputs are pending.
