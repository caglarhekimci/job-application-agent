# Codex model-comparison pilot

This directory contains a synthetic, frozen 24-question pilot derived from `expanded-synthetic-v1.json`. It compares two prompt strategies over the same source facts and output contract:

- **B1** presents a general instruction and CV-like text.
- **B2** presents typed evidence and explicit resolver rules.

`pilot-v1.json` contains scorer-only expected outcomes and source records. The harness strips expected values and rationales from both prompts. `freeze-manifest-v1.json` pins the full dataset, pilot, output schema, and exact rendered prompt hashes. The pilot covers two questions from each of 12 synthetic profile archetypes; it is not 240-case model coverage or evidence of performance on real candidates.

Run the deterministic scorer test without model access:

```powershell
pwsh -NoLogo -NoProfile -File .\evals\model-comparison\test-harness.ps1
```

Run the six-turn comparison with the pinned official CLI and existing ChatGPT login:

```powershell
pwsh -NoLogo -NoProfile -File .\scripts\test-model-comparison.ps1 -Mode Live
```

Live mode removes API credential variables and invokes Codex with an ephemeral session, read-only sandbox, ignored user configuration, no repository, and shell/apps/plugins/multi-agent/web disabled. Raw JSONL, final JSON, timing, hashes, and usage are retained for each turn. The scorer reports strict status/value accuracy, answerable accuracy, appropriate abstention, bad claims, retained values on abstentions, evidence-citation violations, and run failures separately.

`replay/captured-synthetic-v1.json` is an authored scorer fixture, not model evidence. `results/2026-09-20-pilot-v1` preserves six zero-usage schema-rejection attempts. `results/2026-09-20-pilot-v1-schema-v2` contains the six substantive model turns and their rescored report.

Public reports replace the machine-specific executable prefix with `%LOCALAPPDATA%`. `redaction-manifest-v1.json` records the original and public report hashes plus the sole redacted field. Immutable originals remain under the repository's ignored `artifacts/model-comparison/raw` directory and are not part of the public package. No event stream or model response needed redaction.
