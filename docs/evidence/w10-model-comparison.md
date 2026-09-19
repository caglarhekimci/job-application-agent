# W10 B1/B2 Codex comparison evidence

## Result

A frozen synthetic pilot was run through six independent Codex turns on 2026-09-20 (Europe/Istanbul): three B1 turns and three B2 turns. B1 was better on the strict status-and-value metric. This pilot provides no evidence that B2 improved results.

| Strategy | Strict outcome | Answerable correct | Appropriate abstention | Bad claims | Abstentions retaining a value | Evidence allow-list violations | Failed substantive runs |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| B1 general/CV-like | 69/72 | 27/27 | 42/45 | 3 | 0 | 3 | 0 |
| B2 typed evidence/rules | 57/72 | 27/27 | 42/45 | 3 | 12 | 0 | 0 |

The three bad claims in each strategy were the same case in all three repetitions: the model returned the supplied, verified annual-gross salary where the frozen deterministic resolver expectation was `NeedsInput`. They are counted as bad claims because scoring was frozen before output and defines every `Resolved` response to an expected abstention as a bad claim. They were grounded in a supplied source fact, so this result also exposes a benchmark-policy tension: the expected outcome represents the current resolver's intentionally narrow support, rather than absence of the annual-gross fact.

Unknown, missing, unverified, unit-mismatched, sensitive, language-mismatched, and company-scoped cases were not counted as successful resolved answers. B2's lower strict score came from returning the proposed value alongside the correct `RequiresReview` status on 12 cases across two runs. Those remain appropriate abstentions by status but fail exact value matching. B1's three evidence allow-list violations were citations to the Turkish relocation record while correctly abstaining on the German question; no B1 or B2 resolved answer lacked an evidence ID.

## Frozen inputs

The pilot contains 24 questions, two for each of 12 synthetic profile archetypes. It includes known answers, professional-versus-project experience, current-versus-expected salary, monthly-net versus annual-gross units, unverified contacts, language mismatch, zero-length fields, sensitive health data, missing and proposed evidence, and company scope.

The same source facts, questions, evaluation time, and response schema were supplied to both strategies. B1 rendered those facts as CV-like lines. B2 rendered them as typed JSON and added explicit evidence and resolver rules. Expected statuses, expected values, and rule rationales remained scorer-only and were absent from both rendered prompts.

- Expanded dataset SHA-256: `17EDD99D9DCC17C9074EA782F5457957BDD40AF6AE71D84C0AAD55BADA82D7E0`
- Pilot SHA-256: `7EECF1FD78D8147C157845479848985B5D4976398519B776A5C8455EBCAF6E25`
- Output schema SHA-256: `F49A3F040D2A818AAFDFF85D11A6E457F5E8D24FD24A2CC93B0E0A8F0D64EBC3`
- B1 rendered prompt SHA-256: `56AAAFE5384A7F84AAC00F333DC8EFB7C5F20175996A68952D18DD587C8E0571`
- B2 rendered prompt SHA-256: `02F3E04E993AC764406FB849466A50C3A4D7583694FDCD8CC1165C3B55390C1A`
- Fixed scenario time: `2026-09-18T00:00:00Z`

The schema originally included `uniqueItems`. Six requests were rejected with `invalid_json_schema` before inference, with zero recorded usage and no model output. Those failures are preserved in `evals/model-comparison/results/2026-09-20-pilot-v1`. The schema was narrowed to the supported core subset; uniqueness, exact case count, allowed IDs, and status/value semantics remain enforced by the local scorer. The compatibility amendment was recorded before any model output, and the pilot, prompts, facts, and expected outcomes did not change.

## Execution proof

The six substantive turns used the pinned native `codex-cli 0.155.0`, requested model `gpt-6-astra`, and an existing ChatGPT login forced by `forced_login_method=chatgpt`. The CLI JSONL does not expose an independently verified served-model identifier, so the evidence records the requested model and `servedModel: NotExposedByJsonl`.

Each turn used a new temporary directory and `--ephemeral`, `--ignore-user-config`, `--sandbox read-only`, and `--skip-git-repo-check`. API credential environment variables were removed. Shell, apps, plugins, multi-agent, shell snapshot, and web search were disabled; no MCP server was configured. Every substantive event stream contained only thread/turn lifecycle events and one agent message. Temporary runtime directories were removed after capture.

The runs occurred from 2026-09-20 00:52:48 through 00:57:06 Europe/Istanbul. Usage reported by the CLI was:

| Strategy | Input tokens | Cached input tokens | Output tokens |
| --- | ---: | ---: | ---: |
| B1 | 35,186 | 7,552 | 3,775 |
| B2 | 36,495 | 7,552 | 3,805 |
| Total | 71,681 | 15,104 | 7,580 |

No price or cost is estimated because the applicable included-quota pricing is not established by this evidence. No paid API credential was used.

The first scoring pass incorrectly treated two valid-schema B2 responses as whole-run failures because they retained values on abstention. A replay regression reproduced the pattern. The scorer now records those as per-case value mismatches, and `Rescore` mode recomputed the report from unchanged raw JSONL and final responses without new model calls. `scoring-amendment.json` preserves the pre-rescore report hash and both summaries.

Before public packaging, the result trees were copied unchanged to the ignored `artifacts/model-comparison/raw` directory. The two public reports replace only the machine-specific `cli.executable` prefix with `%LOCALAPPDATA%` and declare `executablePathRedacted: true`. `redaction-manifest-v1.json` maps each immutable original report hash to its public-copy hash. A credential and private-path scan found no other value requiring redaction; raw event streams and model responses contain synthetic fixture content only.

## Verification

The deterministic replay test passed 12 assertions across six captured synthetic runs and 144 scored outcomes. It proves exact status/value scoring, bad-claim counting, abstention credit, retained-value handling, prompt expectation non-leakage, and frozen dataset identity. The final report independently contains raw-event and final-output hashes for all six substantive turns.

Relevant artifacts:

- `evals/model-comparison/pilot-v1.json`
- `evals/model-comparison/freeze-manifest-v1.json`
- `evals/model-comparison/replay/captured-synthetic-v1.json`
- `evals/model-comparison/results/2026-09-20-pilot-v1-schema-v2/report.json`
- `scripts/test-model-comparison.ps1`

## Limits

This is a 24-question synthetic pilot, not the 240-question deterministic benchmark and not a real-candidate or real-job evaluation. The 12 profiles are curated archetypes that share implementation fixtures and reusable question families; they are not 12 independent populations or generalization evidence. Three turns per strategy are too few for stable model-quality estimates. The prompt formats and instruction lengths differ, so token usage differs even though source facts and response requirements are held constant. Results characterize these frozen prompts, facts, model route, and time only. No host, browser, application submission, tool-use, or end-to-end job-search capability was tested.
