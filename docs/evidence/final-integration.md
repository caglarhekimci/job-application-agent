# Final alpha.2 integration sequence

Separate checkout: `%TEMP%/jobagent-clean-da84c25`, source
`0e01176ab12b1d9d5ca129215419e0ab2a3e8aa4` plus the final UI-only correction.
The checkout reused the current Windows user's dependency/browser caches; this is
not a fresh-user or fresh-OS installation claim.

`scripts/verify.ps1` initially exited **1**. Frontend, locked restore, Release build
(zero warnings/errors) and formatting passed. Core83 and Document15 passed;
E2E94/95 passed. The document-review checkbox could reset when a delayed parent
refresh caught up with the proposal revision. A concurrent regeneration also left
the checkbox enabled before its response settled.

The correction preserves confirmation for the same revision/status/bundle, ignores
older GET responses and disables review during a mutation. A genuinely changed
revision, bundle or status still invalidates confirmation; regeneration clears it.
Server-side source/hash/revision approval checks are unchanged.

An initial filtered command in the main checkout found **zero matching tests**
because its old Release binaries predated FR-15; this is not counted as validation.
The matching clean-checkout binaries then ran the affected actual Chromium test:
**1 passed**, including regeneration, explicit approval, text download and zero
external requests. Already-passing tests were not repeated. The remaining suites
were resumed, rather than relabeling the failed original command as successful.

| Project | Passing distinct tests | Execution |
|---|---:|---|
| Core | 83 | Initial run |
| Document | 15 | Initial run |
| E2E | 95 | 94 initial passes + 1 corrected targeted test |
| Infrastructure | 41 | Continuation |
| MCP | 22 | Continuation |
| Workspace | 37 | Continuation |
| Total | **293** | No skipped tests; not a single uninterrupted run |

Reports: `artifacts/verification/20260919T233219Z/`, including `continued/`.
The failure remains in the original TRX. Continuation exited **0**: launcher,
initial11/expanded300 fixture outcomes, offline scorer replay12 assertions,
scanner regressions4/4, source/history scan357 files/11 commits, prerequisites and
whitespace checks passed. The installed local Node version was20.14.0; CI uses22.
Exact release CI and archive verification are recorded in the release evidence.

Feature evidence: [FR-15](fr15-document-adaptation.md),
[FR-16](fr16-model-policy.md), [outbound expiry](outbound-expiry.md),
[deterministic challenge test](clean-checkout-fix.md).
The earlier [actual model local12 run](codex-local-service.md) used its documented
build; the later changes were tested locally without another model call.
