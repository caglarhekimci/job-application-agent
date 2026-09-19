# Initial synthetic evaluation dataset

`datasets/initial-synthetic-v1.json` is a small, curated starting set for the
deterministic fixture runner. Every record and the dataset itself declare
`synthetic: true`. The synthetic content was created for this repository and is
provided under the repository's MIT license.

The set contains 11 cases: five development cases and six test cases. Candidate/job
metadata groups and template-family IDs do not cross that split. Each case records an
expected status, value or decision and a human-reviewable rule rationale. The loader
rejects non-synthetic records, duplicate IDs, unknown JSON fields, and group/template
metadata leakage.

All cases execute a small shared set of `SyntheticData` fixtures and rule enums. The
split check prevents accidental identifier reuse; it is **not** evidence of independent
candidate/job populations, holdout generalization, or model quality.

This is an **initial partial dataset**, not the plan's target benchmark of 12 profiles,
60 jobs, 240 question-answer cases, 12 form flows and 40 security cases. It covers a
few high-value deterministic boundaries: verified contact and salary values, unknown
and unverified answers, professional experience thresholds, missing work authorization,
and verified/unverified/unknown submission classification.

The runner hashes the exact dataset bytes with SHA-256 and writes that hash into each
report. Reports contain case IDs, statuses, decisions, match flags, rationales and
actual metric numerators/denominators. They omit expected and actual answer values.
The fixture run makes no model or network call; real-model status remains `NotRun`.
`scenarioAsOf` controls time-dependent facts and rules, while `runAt` records when the
caller ran the evaluation.
