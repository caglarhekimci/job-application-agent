# Public evidence map

Snapshot: 2026-09-19 UTC. Source is public; tagged release pending.

| Claim | Evidence | Limit |
|---|---|---|
| Open source | [Public source and MIT license](https://github.com/caglarhekimci/job-application-agent/tree/da84c258362402e6feed898cfd5f02224af9b7c3) | New project; not an adoption claim |
| Tested source | [Successful Windows CI](https://github.com/caglarhekimci/job-application-agent/actions/runs/35469458955), [137-test local/clean-checkout ledger](../VERIFICATION.md) | Synthetic/local scope, current-user caches in local clean checkout |
| Evidence-grounded answers | [Core rules/tests](../../tests/JobAgent.Core.Tests), [workspace evidence](../evidence/personal-workspace.md) | Deterministic rules; not broad model quality |
| Approval-bound browser | [E2E tests](../../tests/JobAgent.E2E.Tests), [threat model](../THREAT_MODEL.md) | Managed loopback fixture; not native host control or live employer validation |
| MCP and host | [Actual Codex call](../evidence/codex-host.md) | One read-only capability call; full host flow incomplete |
| Distribution | [Plugin package test](../evidence/plugin-package.md) and packaged PDF/DOCX smoke in ledger | Local ZIP; no public store approval |
| Maintenance | Public commits, tests and documented real fixes | No manufactured issues/PRs or long maintenance history |
| Independent users/reuse | 0 observed independent users; no downstream reuse verified | Maintainer tests are excluded |
| LinkedIn | BlockedExternal | No platform authorization |
| OSS program | Form prepared with public evidence | Not submitted; acceptance and benefit unknown |

Private email, Organization ID, CV, account state and application receipt are never
published. Preserve dates, exact commit and sample sizes when updating claims.
[Adoption](../community/ADOPTION.md), [maintenance](../community/MAINTENANCE.md),
[capabilities](../CAPABILITY_MATRIX.md), [gates](../community/PUBLICATION_GATES.md).
