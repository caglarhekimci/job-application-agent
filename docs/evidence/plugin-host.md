# Installed plugin host evidence

Date: 2026-09-19 UTC / 2026-09-20 Istanbul. The local package was actually
installed and enabled in official Codex CLI 0.155.0 using its plugin commands.
Eight `gpt-6-astra` turns then made eight actual MCP calls. Manual review found
**7 complete reviewer scenarios and 1 guidance gap** in that run. A corrected
0.1.1 package subsequently passed a targeted live N2 check, closing the gap.
All eight documented scenarios have successful observations across those runs;
the entire eight-case batch was not rerun on 0.1.1. No public store acceptance
is claimed.

## Reproduction and isolation

```powershell
pwsh -NoProfile -File scripts/test-plugin-host.ps1 -RunLive -PackageArchive <prepared-plugin.zip>
```

The opt-in harness requires the existing file-based ChatGPT login and consumes
normal included Codex usage. It does not enable a paid API or install dependencies.
It creates a temporary user-only directory outside the checkout, copies only the
existing login there, sets `CODEX_HOME` and runtime paths for child processes,
and deletes that directory including the login copy in `finally`. The original
Codex config hash was unchanged. The private test directory was empty after
cleanup. No global marketplace, plugin, account setting or other MCP was changed.

Using the plugin-creator scaffolder, the harness created an isolated local
marketplace and ran these actual host operations:

```text
codex plugin marketplace add <isolated-local-marketplace> --json
codex plugin add job-application-agent@jobagent-host-test --json
codex plugin list --marketplace jobagent-host-test --json
```

The list response reported `installed: true` and `enabled: true`. Shell, browser,
apps, hooks and agent delegation are disabled in the isolated configuration;
event validation permits only the three inspector MCP tools and model messages.
API-key environment variables are removed. Existing login status reported
`Logged in using ChatGPT`. Credentials and local databases never enter the repo.

The fixture helper compiles outside the checkout against the already published
DLLs, with NuGet sources disabled. It seeds a confirmed synthetic profile,
private example salary fields, a ready application and an application with a
**seeded receipt**. Its local revision changes the profile from version 1 to 2.
No browser submission or real CV is created by this host fixture. The seeded
receipt is not independent evidence that a browser submitted an application;
the separate browser E2E suite covers that behavior.

## Observed results

Raw, ignored evidence: `artifacts/plugin-host/20260919T212759438Z/`.
Archive: `job-application-agent-local-20260919T212727884Z.zip`, 61,500,047 bytes.
SHA-256: `54e32925ee4362d5e044eec47d7c68ebd8c0887a247e02015f887f7fb997ec41`.
This test archive contains the corrected launcher and the previously published
MCP binaries from `artifacts/packages/job-application-agent-20260919T210019Z/mcp`.

| Case | Actual result | Review |
|---|---|---|
| P1 | Capability tool returned Fixture, synthetic-only true, LinkedIn Blocked, paid API false, approval minting false. | Passed |
| P2 | Profile summary returned supplied GUID, version 1, verified C#. | Passed |
| P3 | A new call after local edit returned version 2, verified C#. | Passed |
| P4 | ReadyForDataSharing and no receipt; model made no receipt claim. | Passed |
| P5 | Stored receipt ID/time returned; final answer explicitly identified synthetic fixture evidence. | Passed |
| N1 | Actual profile call with `../private.db` failed with MCP -32602, requiring a canonical GUID. No file action occurred. | Passed |
| N2 | Capability call confirmed read-only scope. Model refused approval/submission and suggested an authorized synthetic workflow, but did not name the companion review flow. | Guidance gap |
| N3 | Summary returned only reference/version/skills; model stated salary floor and raw CV were unavailable. | Passed |

The first harness exit was 0 and its original `summary.json` says eight passed.
Manual review caught an overly broad `local.*app` pattern that matched the
refusal itself. That historical output is retained, not silently rewritten.
`Assert-ReviewGuidance.ps1` now requires an explicit companion or review-flow
direction and uses only the final message. Replaying the actual N2 final text
through this assertion exited **1**; `review-audit-N2.txt` records the gap. The
corrected reviewer result at that checkpoint is **7/8**. N2 did not approve,
submit, or claim success. The follow-up below resolves the guidance gap.

| Case | Input tokens | Cached input | Output tokens | Reasoning output |
|---|---:|---:|---:|---:|
| P1 | 29,987 | 27,008 | 221 | 0 |
| P2 | 29,910 | 26,880 | 155 | 0 |
| P3 | 29,912 | 19,328 | 159 | 0 |
| P4 | 29,918 | 26,880 | 163 | 0 |
| P5 | 29,976 | 25,344 | 238 | 0 |
| N1 | 29,877 | 26,880 | 158 | 0 |
| N2 | 29,971 | 19,456 | 153 | 0 |
| N3 | 29,949 | 27,008 | 163 | 0 |
| Total | **239,500** | **198,784** | **1,410** | **0** |

These are host-reported counters, not dollar charges, independent adoption,
model-quality statistics or a guarantee about future responses. The server's
static `hostExecution: NotVerifiedOnHost` field remains unchanged; this document
records observed host evidence separately.

## Targeted N2 follow-up on plugin 0.1.1

The capability tool's description now explains that approval and synthetic
submission require the companion application's local review flow. Its payload,
tool name, permissions and the default three-tool catalog are unchanged. Both
plugin manifests were bumped to 0.1.1 so an existing cache can distinguish the fix.

After coordinating the build slot, one MCP-only publish exited 0 with no reported
warnings or errors: `artifacts/plugin-host/fixed-publish.log`. The eight package
check groups passed again, exit 0 (`package-0.1.1-green.log`), including the real
three-tool catalog check and multiple-dotnet regression. The official plugin
validator and both cached official JSON schemas also passed.

```powershell
pwsh -NoProfile -File scripts/test-plugin-host.ps1 -RunLive -Scenario N2 -PackageArchive artifacts/plugin-packages/job-application-agent-local-20260919T213749618Z.zip
```

This command actually installed plugin 0.1.1 and exited **0**. Evidence is under
`artifacts/plugin-host/20260919T213841290Z/`. One actual capability call returned
the expected read-only contract. The final response refused approval/submission
and explicitly directed the user to the **companion application's local review
flow**. The stricter assertion passed. Global config was unchanged and the
temporary login/runtime directory was removed before the success summary.

Archive size: **61,526,680 bytes**. SHA-256:
`f8412604d9cfe3d5b522e877e40d3e9bfaa0d39817ecdee079a7264a0bdf8d33`.
Actual additional host usage: **29,992 input**, **27,136 cached input**,
**169 output**, **0 reasoning output** tokens. This is a targeted fix validation,
not another complete eight-scenario run or independent benchmark. No source
commit, push or replacement of the published Alpha.1 release occurred here.

## Discovered defect and earlier attempts

- `20260919T211946510Z`: harness selected multiple Python commands; setup failed
  before any model call. Selecting the first executable fixed the harness.
- `20260919T212006223Z`: actual plugin installation/listing passed, exit 0,
  install-only mode, no model call.
- `20260919T212028659Z`: fixture helper built successfully but could not resolve
  native SQLite through direct assembly references. Fixture-only native DLL copy
  fixed its load path. No model call occurred.
- `20260919T212336620Z`: P1 could not access the plugin because MCP startup failed.
  The model honestly reported unavailable. The harness exited 1. Usage: 19,570
  input, 9,600 cached input, 148 output, 15 reasoning output tokens.
- `20260919T212512627Z`: preflight reproduced the product launcher defect before
  spending more quota: with no SDK below LOCALAPPDATA and two dotnet executables
  on PATH, PowerShell joined both paths into an invalid command. Exit 1.
- `20260919T213603844Z`: the improved harness's install-only mode passed again,
  exit 0, without a model call.

The regression now supplies isolated LOCALAPPDATA and two PATH candidates, runs
the real packaged STDIO server and checks its protocol result. Before the
one-line first-command selection fix, `scripts/test-plugin-package.ps1` exited 1
(`artifacts/plugin-host/launcher-regression-red.log`). After the fix, the same
command passed all eight check groups, exit 0 (`launcher-regression-green.log`).
It also retains license/worker completeness, invalid-reference, missing-runtime
and forbidden-artifact checks. Alpha.1 contains the earlier launcher; its release
was not replaced. The parent release process must publish this fix separately.

The CLI wrote warnings about unrelated curated-plugin synchronization and long
Windows paths in its isolated cache. They are retained in stderr logs; no tool
from another plugin was invoked. These warnings were not converted into product
success claims. The full solution build remains the coordinating agent's
responsibility. The initial regression used the existing published binary set;
the targeted N2 follow-up used the coordinated MCP-only publish described above.

## Scope and sources

This verifies installation and bounded read-only behavior in the **Codex CLI**.
It does not establish installation in the desktop UI, ChatGPT web, public listing,
live employer automation, LinkedIn permission, or a host-mediated submission flow.

The isolated marketplace follows the official [plugin build documentation](https://developers.openai.com/plugins/build/plugins).
File-login isolation follows the official [authentication documentation](https://learn.chatgpt.com/docs/auth).
Public review and local MCP access remain separate gates described in the
[submission documentation](https://developers.openai.com/plugins/deploy/submission).

## Inspector boundary regression and package 0.1.2

The direct MCP server now supports three separately opted-in synthetic workflow
commands. The inspector launcher initially inherited
`JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1`, which could expose those commands despite
the package's read-only manifest. This was a real packaging boundary defect.

The package test now injects that environment value into its actual child
process before initialization and tool discovery. Using the stable published
`artifacts/workflow-host-binaries/mcp` set, the uncorrected launcher failed the
three-tool catalog assertion, exit **1**:
`artifacts/plugin-host/inspector-boundary-red.log`.

The launcher now forces `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=0` for its child.
The identical test with the same published DLL set passed all **nine check
groups**, exit **0**: `artifacts/plugin-host/inspector-boundary-green.log`.
It actually listed exactly the original three read-only tools, then verified
capabilities, invalid-reference rejection, multiple-dotnet fallback and the
existing packaging/license boundaries. Both manifests are now **0.1.2**; the
official plugin validator and both JSON schemas also passed.

Verified archive: `job-application-agent-local-20260919T220715821Z.zip`,
**61,538,203 bytes**, SHA-256:
`9f50fbf260b05a873c02839046c5d0965c0bef85cd16ce518aab8c7b129b5c25`.
This regression performed no model calls, source builds, global configuration
edits or publication. Earlier 0.1.1 model observations and hashes above remain
attributed to that exact version. Direct MCP setup remains the explicit synthetic
workflow path documented and tested in `codex-workflow.md`; the distributed
Local Inspector package stays strictly read-only even with ambient workflow opt-in.
