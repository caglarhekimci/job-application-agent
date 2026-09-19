# Maintenance evidence

**Snapshot:** 2026-09-20
**Current public maintenance history:** Source and regression fixes are public,
including [the shared-service checkpoint](https://github.com/caglarhekimci/job-application-agent/commit/5b65ed7b8f0fe3639d12a5a45b3dce06689a9e06).
There is no established independent issue/PR history or sustained maintenance record yet.

Initial implementation and regression work is real, but is not an established public maintenance burden. Actual fixes include the Release document-worker test configuration and personal-form state preservation; see the verification ledger and personal-workspace evidence.

## Actual internal review fixes

These are maintainer/agent findings, not independent user reports or manufactured
GitHub issues. They do not increase the external-report metrics below.

| Finding | Reproduction / correction | Public source | Release status |
|---|---|---|---|
| Launcher selected more than one `dotnet` executable on PATH | Actual installed-host discovery and package regression; [evidence](../evidence/plugin-host.md) | ac53072 | v0.1.0-alpha.2; alpha.1 retained |
| Expired evidence and incorrect legacy application-scope identity | Real failing tests, strict expiry and draft-GUID binding; [review record](../evidence/common-service-review.md) | 5b65ed7 | v0.1.0-alpha.2 |
| Deleted private data retained in a recovery snapshot | Separate failing profile/workspace tests, followed by backup invalidation; [store record](../evidence/store-recovery.md) | 5b65ed7 | v0.1.0-alpha.2 |
| Late-challenge test assumed a 750ms approval window | Failed clean-checkout run; deterministic phase-controlled correction passed13 focused tests | 0e01176 | v0.1.0-alpha.2 |
| Approval could expire during asynchronous browser work | Controlled-clock RED/GREEN tests; final outbound receipt check and answer deadline binding | 0e01176 | v0.1.0-alpha.2 |
| Document confirmation reset during background refresh | Actual clean-checkout failure; revision/status/hash-bound UI correction and focused browser pass | See final integration evidence | v0.1.0-alpha.2 |

## External maintenance-entry template

| Evidence ID | Opened | Closed/released | Trigger | Public issue | Reproduction | Fix/PR | Regression test | Release | Maintainer action | Limitations |
|---|---|---|---|---|---|---|---|---|---|---|
| _none_ | — | — | — | — | — | — | — | — | — | No public maintenance evidence yet |

## Required lifecycle

1. Record the real report or dependency/security event.
2. Reproduce it with synthetic/redacted data.
3. State severity and scope without inflating user impact.
4. Add a meaningful failing regression test when appropriate.
5. Review and merge the fix with human responsibility for correctness.
6. Link the release note or explain why no release was needed.
7. Close the loop with the reporter when contact and consent allow it.

## Maintenance metrics

Track definitions before values:

| Metric | Definition | Exclusions | Current value |
|---|---|---|---:|
| Reproducible external issues | Independent reports reproduced by the maintainer | Internal review notes, duplicates, synthetic seeded issues | 0 |
| Tested fixes | Closed issues with a relevant regression check | Formatting-only changes and unrelated tests | 0 |
| Published maintenance releases | Releases after the initial public release that contain real fixes | Local tags, drafts, rebuilt identical artifacts | 1 (alpha.2) |
| Median response time | Time from public report to first substantive maintainer response | Spam and bot-only alerts | Not available |

Do not create empty issues, superficial pull requests, or artificial release churn to populate this file.
