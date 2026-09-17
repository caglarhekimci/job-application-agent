# Verification ledger

All entries distinguish environment checks, expected failing tests, passing tests
and external actions. No test has yet run. CI/live/model evaluation: NotRun.

## W00 — environment (2026-09-18)
- Parent: not a git repository (`git rev-parse --show-toplevel`, exit 128).
- Git 2.47.1.windows.2; Node 20.14.0; npm 10.7.0; PowerShell 7 available.
- System .NET SDKs: 8.0.319 and 9.0.103. Per-user .NET10 SDK installed: 10.0.401, exit 0.
- `codex --version`: 0.44.0, exit 0. Do not assume current CLI host features.
- `gh auth status`: not logged in, exit 1; connector authenticated as caglarhekimci.
- Connector repository lookup: 422 absent or inaccessible; existence not asserted.
- No remote configured or created. New local branch: work/local-synthetic.
- Existing Chromium cache found; compatibility and actual launch not yet tested.
- Source documents read in full (master: 1,564 lines; execution prompt: complete).

SDK: official win-x64 ZIP, SHA512 matched release metadata, extraction and
`%LOCALAPPDATA%/JobApplicationAgent/tools/dotnet/dotnet.exe --version` exit 0.
Release metadata: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
Sources: docs/research/verified-sources.md. Commit: first local checkpoint.
No tests skipped or represented as passing.
