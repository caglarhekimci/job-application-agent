# FR-16 local model proposal policy

Date: 2026-09-20

This slice adds a durable, zero-paid policy for model-authored proposals in the protected personal workspace. The default is `HostMediated`, paid API access is always false, and each application defaults to four successful answer-proposal operations with a hard configurable maximum of ten.

## Enforced behavior

- The policy and per-application counters are part of the existing DPAPI-protected workspace payload. There is no API key, provider credential, model name, price, token, or external quota field.
- `HostMediated` permits the three existing host proposal entry points. Profile and job proposals remain pending local review. Application answer proposals also remain pending local review and consume one operation per successfully persisted request.
- An answer-proposal request consumes one operation even when every proposed answer abstains. Invalid, stale, rejected, or failed-to-save requests consume none. The counter and proposal update are saved under the same workspace lock and revision write.
- At `used == limit`, application answer proposals fail with `BudgetExceeded`. Counters survive process restart and are not reset when the user raises, lowers, or changes the policy.
- `Fixture` rejects profile, job, and application host proposal writes with `ProviderModeDisabled`. Manual review and answer entry through the local UI remain available.
- `Api` cannot be selected. UI and service updates fail with `PaidApiDisabled`; corrupted null, unknown-enum, out-of-range, negative-use, and expanded bridge claims fail closed.
- The local companion capability response is read-only. It reports the stored mode, the local per-application operation limit, and `paidApiEnabled: false`. It exposes no policy mutation tool.

The cookie/CSRF-protected UI exposes `GET` and `POST /api/workspace/model-policy`. The POST parser accepts exactly `expectedRevision`, `mode`, and `maxAnswerProposalOperationsPerApplication`; it rejects duplicate, missing, null, unknown, oversized, or wrongly typed fields. The settings screen allows inspection without another confirmation and persists changes with the workspace revision.

## Acceptance boundary

This is a local proposal-write budget. It does not select or constrain the Codex model, tokens, ChatGPT/Codex account quota, host-side retries, latency, or external cost. A host invocation may already have used external quota before the companion accepts or rejects its proposed write. Profile and job proposal writes are mode-gated but are not charged to the per-application answer-proposal counter. There is no paid API implementation and no provider fallback.

## Test-first evidence

The initial Core and Workspace runs failed to compile because the policy types and workspace APIs did not exist. The initial endpoint and browser runs failed because the route and settings panel did not exist. After implementation:

- `JobAgent.Core.Tests`: 70 passed, 0 failed (`artifacts/fr16/green-core/fr16-core-green.trx`).
- `JobAgent.Workspace.Tests`: 33 passed, 0 failed (`artifacts/fr16/green-workspace/fr16-workspace-green.trx`). These cover exact limit, restart persistence, concurrent revision serialization, all-abstain charging, no reset on policy changes, all three Fixture host-write gates, manual UI review, and invalid/null policy handling.
- Strict policy endpoint: 1 passed, 0 failed (`artifacts/fr16/green-endpoint/fr16-endpoint-green.trx`).
- Host bridge policy validation: 5 passed, 0 failed (`artifacts/fr16/bridge-validation/fr16-bridge-validation.trx`).
- Existing local 12-tool protected-workspace flow: 1 passed, 0 failed (`artifacts/fr16/local12-regression/fr16-local12-regression.trx`).
- Real Chromium settings update and reload: 1 passed, 0 failed (`artifacts/fr16/green-ui/fr16-ui-green.trx`). No request left the local dashboard origin.
- `web`: TypeScript check and production Vite build passed.

No model, paid API, external job site, or remote provider was called by this slice.
