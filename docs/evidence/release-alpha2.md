# Alpha.2 release evidence

Release source: `4d475bbcb2bc43b42a7c4c82eb28ce48eff28464`.
[v0.1.0-alpha.2](https://github.com/caglarhekimci/job-application-agent/releases/tag/v0.1.0-alpha.2)
was published on 2026-09-19 at 23:55:50 UTC (20 September in Istanbul).
Exact-source [Windows CI 35477129404](https://github.com/caglarhekimci/job-application-agent/actions/runs/35477129404)
completed with conclusion **success**: **293 passed, 0 failed, 0 skipped** in one
complete run (Core 83, Document 15, E2E 95, Infrastructure 41, MCP 22, Workspace 37).
Build had zero warnings/errors; the full verification and prerequisite steps passed.
CI reports: `artifacts/verification/20260919T235101Z/` on the hosted runner.

The separate checkout was clean before packaging. `scripts/package.ps1`,
`scripts/test-package.ps1` and `scripts/test-plugin-package.ps1` all exited **0**.
Checks covered every manifest hash, authenticated local UI access, PDF/DOCX parsing
through the packaged worker, relocated STDIO initialization, exactly three read-only
plugin tools despite inherited command flags, multiple-dotnet PATH selection,
missing-runtime behavior and rejection of private/incomplete package input.

| Archive | Bytes | SHA-256 |
|---|---:|---|
| job-application-agent-20260919T234917Z.zip | 123021611 | 2ACF32F209D1545C337A99D4913B2EDCD42FA7780985EE725B6B1FCB93F0774B |
| job-application-agent-local-20260919T234949597Z.zip | 61644709 | 2B3115EE6749BB746CA4F5A39B049CA9C555F0ED180BBFF0CD8094A657B911E1 |

Both archives have SHA-256 sidecar files. Captured package/check output is retained
locally under `artifacts/alpha2-final-*.log`.
Unauthenticated GitHub REST confirmed release ID 392255688, draft=false,
prerelease=true, four attached assets and both ZIP digests matching the local
hashes above. The release page confirms tag target 4d475bb. The public receipt is
retained locally in `artifacts/alpha2-public-receipt.json`.

The [local sequence](final-integration.md) covers293 distinct passing tests after
one focused document-UI correction. The first public candidate failed on a separate
driver-cleanup race; [its record](ci-websocket-cleanup.md) and focused10/10 pass are
retained. Candidate archives from `f3f38dd` are superseded and were not published.
The existing alpha.1 assets are unchanged. No additional model call or paid API
request was needed for these final corrections or package checks.
Only documentation was updated after the release commit; its publication commit
skips redundant CI. The archives and successful CI stay bound to 4d475bb.

These are Windows prototype checks using synthetic input. They do not establish
independent adoption, a security audit, a live employer submission, ChatGPT store
approval or a Codex for OSS award. Current-user dependency caches were reused
locally; CI starts on a GitHub-hosted Windows runner.
