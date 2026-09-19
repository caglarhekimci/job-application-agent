# W10 deterministic evaluation evidence

Date: 2026-09-18
Scope: deterministic fixture runner and initial synthetic dataset only
Real model/API/host calls: **NotRun**

## Implemented slice

- `FixtureEvaluationRunner.RunFile(datasetPath, options)` executes existing Core answer
  and job rules plus bounded submission-outcome fixtures. It performs no network or
  model call.
- `EvaluationRunOptions` takes a caller-supplied code revision and fixed
  `ScenarioAsOf`; optional `RunAt` records the real invocation time and defaults to
  current UTC. Reports identify mode `fixture`, model/rule engine
  `deterministic-rules-v1`, and prompt/rule contract `fixture-rules-v1`.
- `EvaluationDatasetFile` hashes the exact input bytes with SHA-256 and rejects unknown
  JSON fields, non-synthetic records, duplicate case IDs, and development/test reuse of
  group or template IDs.
- `EvaluationJson.Serialize` emits metadata, case IDs, statuses/decisions, rule
  rationales, and match flags. It does not emit expected or actual answer values.
- Answer correctness and appropriate abstention are separate metrics. An abstention is
  never credited as a correct answer. Unknown and unverified submission fixtures are
  classified `NotSuccess`; the safety metric counts any success in those cases as a
  violation.

## Dataset and actual fixture result

Dataset: `evals/datasets/initial-synthetic-v1.json`
Schema: `1.0`, `synthetic: true`, MIT
Exact SHA-256: `5DD52BBED88A58ADCBBFD1B136BA481C74C6211C24C737DC1D0EFAB7E51BE3DC`
Size: 11 curated cases (5 development, 6 test)

| Metric | Numerator | Denominator | Meaning |
|---|---:|---:|---|
| `correct-answer` | 3 | 3 | Exact status/value on answerable deterministic fields |
| `appropriate-abstention` | 2 | 2 | Expected unknown/unverified answer boundaries |
| `deterministic-outcome` | 6 | 6 | Exact job status or submission status/decision |
| `unsafe-submission-success` | 0 | 2 | Success classifications for unknown/unverified submissions; lower is safer |

These are deterministic fixture results, not model quality. The cases share a small set
of `SyntheticData` profiles and enum-selected scenarios. Disjoint group/template IDs are
a bookkeeping validation against metadata leakage; they do not establish independent
candidate/job populations, holdout generalization, or real-world performance.

This is an initial partial dataset. It is not the plan's target of 12 profiles, 60 jobs,
240 question-answer cases, 12 form flows, and at least 40 security cases. No counts were
manufactured to satisfy those targets.

## TDD and verification

Raw logs are ignored under `artifacts/evaluation/`.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~EvaluationTests"` | 1 | RED: evaluation namespace/types absent (`core-red.log`) |
| same filtered command after the initial implementation | 0 | GREEN: 11 selected tests, including 7 new evaluation tests and 4 existing job-evaluation tests (`core-green-filtered.log`) |
| full Core.Tests project | 0 | 31 passed (`core-green-full.log`) |
| `dotnet test ... --filter "FullyQualifiedName~JobAgent.Core.Tests.EvaluationTests."` after review tests | 1 | RED: separate `ScenarioAsOf`/`RunAt` contract absent (`core-review-red.log`) |
| same exact-class filter after correction | 0 | 7 passed (`core-review-green.log`) |
| final full Core.Tests project | 0 | 31 passed, 0 failed, 0 skipped (`core-final.log`) |

Required named tests are present:

- `AbstentionIsNotCountedAsCorrectAnswer`
- `UnknownSubmissionIsNotSuccess`
- `TestSplitHasNoTemplateLeakage`

No full solution build, real-model comparison, live platform run, or global format was
performed in this slice. The parent integration owns the CLI command and full release
verification.
