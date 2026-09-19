# Verification ledger

## Public source and independent checkout — 2026-09-19 UTC

Public main is commit `da84c258362402e6feed898cfd5f02224af9b7c3`.
Unauthenticated GitHub REST confirmed public visibility and main; push exited 0.
[GitHub Actions run 35469458955](https://github.com/caglarhekimci/job-application-agent/actions/runs/35469458955)
completed with conclusion **success** for that exact commit.
Private vulnerability reporting was enabled and its saved setting verified.

A separate clone at `%TEMP%/jobagent-clean-da84c25` ran the README path:
`scripts/bootstrap.ps1`, `scripts/verify.ps1`, `scripts/doctor.ps1`; all exited 0.
The clone stayed clean. Verification reports:
`artifacts/verification/20260919T210809Z/` in that clone.
**137 passed, 0 failed, 0 skipped**, 0 build warnings/errors; launcher, fixture
evaluation, source scanner regressions and 197-file/4-commit history scan passed.
This used the current Windows user's dependency/browser caches; it is not a fresh
Windows account test or an independent user's adoption.

## Integrated local release candidate — 2026-09-19 UTC

`scripts/verify.ps1` exited **0**, reports `artifacts/verification/20260919T205804Z/`.
Release build had 0 warnings/errors; locked restore, frontend build and formatting
passed. Core **42**, Document **15**, E2E **36**, Infrastructure **25**, MCP **12**,
Workspace **7**: **137 passed, 0 failed, 0 skipped**. Launcher, 11-case fixture
evaluation, four source-scan regressions, source/history scan and whitespace passed.
The preceding `20260919T205649Z` run failed three document tests because its helper
project was built in Debug by the solution while Release tests expected Release.
Adding that helper project to the solution fixed the configuration mapping.

`scripts/package.ps1` exited 0, created
`artifacts/packages/job-application-agent-20260919T210019Z.zip`, SHA-256
`E2B491E027155613AEC56661E28DCDD2214DB940147414EB618DB52BCC0A58B2`.
`scripts/test-package.ps1` verified every manifest hash and imported generated
PDF/DOCX through the packaged CLI HTTP API and adjacent worker; exit 0.
Packaged launcher smoke and `doctor.ps1` also exited 0. This package predates final
publication metadata; regenerate the eventual release archive from the final commit.

Actual ChatGPT-authenticated Codex CLI 0.155.0/gpt-6-astra invoked the capability
tool successfully; see `evidence/codex-host.md` for real events and usage. This is
not model-quality evaluation or a complete host-mediated application.
W03/W04/personal-workspace and plugin evidence contain their separate RED/GREEN
and scope limitations. Full W00-W18 completion is not claimed.

All entries distinguish environment checks, expected failing tests, passing tests
and external actions. Early pending notes are historical; later integrated results
supersede them. Live/model-quality evaluation: NotRun. Public CI passed as above.

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

## W02-W12 integrated checkpoint — 2026-09-18

Executed `pwsh -NoProfile -File scripts/verify.ps1`; actual reports are under
`artifacts/verification/20260918T005311Z/`. Frontend build, locked NuGet restore,
Release build (zero warnings/errors) and formatter validation passed. Results:

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Core | 31 | 0 | 0 |
| Infrastructure | 15 | 0 | 0 |
| E2E (real Chromium and React UI) | 35 | 0 | 0 |
| MCP (includes actual STDIO child process) | 12 | 0 | 0 |
| Total | **93** | **0** | **0** |

CLI launch smoke passed dashboard/assets, paired fixture site and exclusive runtime
lock. Four source-scan regressions passed (PEM, P12, extensionless key, removed
historical key). Source/history scan passed 149 candidate files and 3 commits.
Fixture CLI evaluation passed: answers 3/3, appropriate abstentions 2/2, deterministic
outcomes 6/6, unsafe submission success 0/2. Report records dataset hash, actual run
time, fixed scenario date and code revision with +dirty. These are fixture counts,
not model performance or independent holdout. See `docs/evidence/evaluation.md`.

The overall first verify script exited 1 on Markdown trailing whitespace only.
That whitespace was corrected; subsequent `git diff --check` returned exit 0.
The nonzero original run is retained, not relabeled. Packaging/fresh-checkout tests
are still pending. Screenshots in `artifacts/screenshots/` were actually captured
and visually inspected; no browser page errors occurred in the happy path.

Foundation RED/GREEN details: `docs/evidence/foundation.md`.
MCP RED/GREEN details: `docs/evidence/mcp.md`.
Review: `docs/evidence/integrated-review.md`.

Adverse cases reproduced redirect-before-check, early page-script POST, duplicate
transport POST after response loss, malformed receipt, leaked session ID, and
tampered outgoing salary. `artifacts/test-results/boundary-red/boundary-red.trx`
contains 4 failed/5 passed; subsequent initial run had 34/35 due to a Playwright
WebSocket cleanup exception. Scoped cleanup was corrected; the final integration
above passes a strengthened test requiring policy denial AND zero trap requests.
The WebSocket egress test initially passed before that control, so it is not
represented as a demonstrated pre-fix leak. State omits approval credentials;
sharing receipts are consumed; uncertain post-claim outcomes cannot become Cancelled.

Registry audits on 2026-09-18: npm full audit exit 0, zero reported vulnerabilities;
NuGet transitive audit exit 0, no vulnerable packages reported across 10 projects.
`docs/evidence/DEPENDENCIES.json` contains 180 dependency records, including 50
uninstalled optional platform packages whose licenses still need publication review.
These scans are not a security or legal audit.

## Real host attempt and changed authorization — 2026-09-19

Legacy CLI 0.44.0 initially rejected xhigh configuration; a per-invocation override
allowed status verification (`Logged in using ChatGPT`). Its model request then
failed with HTTP 400: gpt-6-astra requires a newer Codex version. No successful tool
call or model result is claimed for that attempt. Logs are private under artifacts.
Isolated official @openai/codex 0.155.0 installed per-user; global install/config
unchanged. Current host smoke pending. Real-model quality evaluation remains NotRun.

User explicitly authorized public repo push and OSS application submission on
2026-09-19. No public repo exists yet (connector returned 404). Browser requires
GitHub sign-in; form name/email/optional organization fields are pending user input.
