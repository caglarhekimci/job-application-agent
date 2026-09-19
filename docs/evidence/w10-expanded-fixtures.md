# W10 expanded synthetic fixture evidence

Date: 2026-09-20

## Delivered dataset and runner

The expanded dataset reaches the section 14 profile, job, and question targets:

- 12 synthetic profile groups and 12 distinct profile archetypes.
- 60 synthetic job cases: five distinct job scenarios for each profile.
- 240 synthetic question cases: 20 distinct question scenarios for each profile.
- Dataset SHA-256: `17EDD99D9DCC17C9074EA782F5457957BDD40AF6AE71D84C0AAD55BADA82D7E0`.

The source file has 20 development-only and 20 test-only question families plus five development-only and five test-only job families. Each stores its rationale once and an explicit expected status/value for all six profile archetypes in its split. `ExpandedEvaluationDatasetFile.Load` materializes 300 individually identified cases. Validation requires the complete split-specific matrices, unique archetypes, scenarios, and actual template families, exact target counts, synthetic declarations, expected statuses, rationales, and disjoint development/test profile and template families.

`ExpandedFixtureEvaluationRunner.RunFile(path, options)` records the supplied code revision, scenario time, run time, mode ID, model ID, prompt ID, and exact dataset hash. Serialized results contain case IDs, status, match flags, split, scenario, and rationale; expected and actual answer values are omitted.

## Test-driven and run evidence

The initial focused RED failed at compilation because the expanded report/loader/runner contracts did not exist. Exit code was 1 (`artifacts/w10-expanded-fixtures/red.log`).

The first implementation materialized all cases and passed four of five tests, but B0 matched 238/240 question expectations. A diagnostic run identified the expired-evidence archetype as the shared cause: its validity endpoint equaled scenario time, while the stable profile policy treats that endpoint as inclusive (`artifacts/w10-expanded-fixtures/mismatch-diagnostic.log`). Setting the synthetic expiry one tick before scenario time aligned the fixture with the existing rule.

A later metadata RED confirmed that the expanded report did not yet expose model and prompt IDs separately (`artifacts/w10-expanded-fixtures/metadata-red.log`). A split audit then identified that different development/test IDs still represented the same underlying rule templates. New failing compilation checks required every family to own one split (`artifacts/w10-expanded-fixtures/split-audit-red.log`). The dataset was rebuilt with 40 distinct question scenarios and 10 distinct job scenarios rather than renamed cross-split copies. Final focused validation and deterministic execution passed 6/6 with no failures or skips (`artifacts/w10-expanded-fixtures/green-focused-final.log`). The complete Core test project passed 58/58 with no failures or skips (`artifacts/w10-expanded-fixtures/core-full-final.log`).

The B0 run used fixed scenario time `2026-09-18T00:00:00Z`, fixed run time `2026-09-20T12:00:00Z`, and test code-revision input `test-revision`. Its actual metrics were:

| Metric | Numerator | Denominator |
|---|---:|---:|
| B0 question outcome | 240 | 240 |
| B0 answerable correct | 110 | 110 |
| B0 appropriate abstention | 130 | 130 |
| B0 job outcome | 60 | 60 |

B0 status was `Completed`. B1 and B2 were `NotRun`.

## Interpretation and remaining targets

These are deterministic regression results over authored synthetic fixtures. They are not model-quality results, real-user outcomes, or independent holdout/generalization evidence. Profile and reusable template families do not cross the split, but both partitions still execute the same Core implementation and related rule categories.

The 12 local form-flow and at least 40 security/negative-case targets in section 14 are separate E2E/security work and are not counted here. No external network, model host, paid API, token usage, or model run occurred. A later B1/B2 comparison must use separately authorized real model execution, record the actual code/model/prompt versions and costs, and preserve B1/B2 as `NotRun` until that happens.
