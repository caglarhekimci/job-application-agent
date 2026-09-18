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

## W01 — real local browser (2026-09-18)
- `dotnet restore JobAgent.slnx`: exit 0, log artifacts/logs/w01-restore.log.
- `dotnet build JobAgent.slnx --no-restore`: exit 0, 0 warnings/errors.
- `tests/JobAgent.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium`: exit 0.
- Browser: Chrome for Testing 151.0.7922.34, Playwright 1.62.0, revision 1234.
- RED `dotnet test tests/JobAgent.E2E.Tests --no-build --logger trx`: exit 1;
  2 smoke failures (missing form and /health 404), 10 separately added approval tests passed.
  Actual report: artifacts/test-results/w01-red/w01-red.trx; log artifacts/logs/w01-red.log.
- GREEN `dotnet test tests/JobAgent.E2E.Tests --logger 'trx;LogFileName=w01-w06-green.trx'
  --results-directory artifacts/test-results/w01-w06-green`: exit 0;
  17 passed, 0 failed, 0 skipped (2 smoke, 10 approval, 5 persistence).
- Chromium actually filled two steps, uploaded synthetic-resume.txt and received SYN receipt.
  Site server record asserts salary 100000, professional years 3 and uploaded filename.
- Commit: see local commit `feat: add executable synthetic site and browser smoke tests`.

## W06 — partial approval/journal foundation
- RED `dotnet test tests/JobAgent.E2E.Tests --filter FullyQualifiedName~ApprovalTests`:
  exit 1; 9 failed/1 passed. GREEN: exit 0, 10 passed. Reports w06-red and w06-green.
- Persistence first compile hit xUnit2031 (test assertion API); corrected before counting RED.
- RED persistence assertions: exit 1, 5 failures (missing persistence/claim/duplicate behavior).
  Logs artifacts/logs/w06-storage-red-assertions.log. GREEN included in 17 above.
- Two journal instances raced to claim one submission: one claim succeeds, receipt consumed
  in the same conditional database update. Recovered Submitting becomes SubmittedUnverified.
- This proves local state control, not exactly-once delivery to external employers.
- Application journal is currently **synthetic-only** and rejects non-synthetic creation.
  Real profile encryption is a separate W02 component under development.
