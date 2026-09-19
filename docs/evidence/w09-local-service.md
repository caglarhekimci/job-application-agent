# W09: shared local workspace and twelve-tool STDIO contract

Date: 2026-09-20. This evidence covers local protocol and application integration,
using synthetic candidate data only. It is not a new model-host run, live employer
submission, platform permission, or store review result.

## Implemented boundary

`ILocalWorkspaceHost` is a typed projection over the same `LocalWorkspace` instance
and protected `personal/workspace.db` payload used by the authenticated local UI.
The MCP process does not create a second profile/job/application data store.
The companion service owns opaque profile, imported-resume, reviewed-job and
application references. Tools accept canonical nonempty GUID references, not paths.

| Process configuration | Registered tools | Scope |
|---|---:|---|
| Both command flags unset/0 | 3 | Existing read-only fixture inspector |
| `JOBAGENT_ENABLE_SYNTHETIC_COMMANDS=1` | 6 | Existing fixed synthetic browser workflow |
| `JOBAGENT_ENABLE_LOCAL_COMMANDS=1` | 12 | Protected local workspace proposals and application preparation |
| Both flags 1 | None | Startup rejects the conflicting configuration |

The local mode needs the flag on both the companion CLI and MCP process, with the
same `JOBAGENT_RUNTIME_DIR`. The default does not change. The distributed inspector
launcher forces both flags to 0; the package regression driver now sets both flags
to 1 in its child environment to exercise that boundary on the next package build.
That updated package test has not been run in this work slice.

The twelve local tools are `runtime_get_capabilities`, `profile_get_summary`,
`profile_propose_patch`, `job_import_text`, `job_evaluate`,
`application_create_draft`, `application_get_questions`,
`application_propose_answers`, `application_prepare_review`,
`application_execute_approved`, `application_get_status`, and `application_cancel`.

Capabilities can discover safe current references. Profile output is limited to
professional skills and verified evidence references; no raw CV, source spans,
contact data, private minimum salary or approved answer payload is returned.
Profile patches and answer proposals stay pending. Answer proposals bind to the
observed workspace revision and use the existing strict evidence validator.
Job import accepts provided text and optional metadata; it never visits the URL
and does not replace the current reviewed job. Application creation uses the
questions stored/reviewed in the local UI, not a model-supplied form schema.

Personal execution always returns `BlockedPermission`: this workspace has no
authorized live browser adapter. No tool can mint consent or approve data. The
existing synthetic adapter retains its separate, tested UI approval/receipt flow.

## Transport checks

Only the protected, expiring loopback registration supplies the bridge origin and
token. The client constructs fixed `/internal/mcp/local/v1/` routes, disables proxy,
cookies and redirects, uses HTTP/1.1, and never retries an uncertain execution.
The server keeps the existing literal-loopback Host/port, remote-loopback, no-Origin,
token, expiry, POST-only and no-query rules. Local and synthetic credentials do not
enable each other's route families. They do not authorize the cookie/CSRF UI routes.

Only five exact local operation shapes allow JSON request bodies: profile patches,
job import, job evaluation, application creation, and answer proposals. The other
routes remain bodyless. JSON bodies require a known content length and JSON content
type, reject transfer/content encoding, are capped at 262,144 bytes and depth 8, and
reject unknown or duplicate keys. Nested tool payloads receive the same strict
unknown/duplicate-field validation before a companion call. Collections and text
have additional field-specific bounds. No approval flag, session credential,
document path or arbitrary request route is accepted.

Responses are capped at 262,144 bytes. The client rejects unknown/duplicate fields,
wrong entity references, invalid/null typed values, unexpected personal submission
evidence and a review reference pointing at a different application. Errors expose
bounded policy codes, not returned payloads or bridge credentials.

## Actual RED/GREEN evidence

The feature baseline used the unchanged published MCP binary with the new local
flag set to 1. Actual STDIO discovery still returned only the three inspector tools
instead of twelve. `artifacts/local-mcp-red-catalog.log` records the observed names
and failed expectation. This baseline harness did not build or mutate that binary.

The first new source tests passed:

- `LocalToolContractTests`: 3/3; local twelve/default three/legacy six catalogs.
  `artifacts/test-results/local-mcp/local-contract.trx`, exit 0.
- `LocalBridgeTests`: initial 3/3; shared workspace and all twelve actual STDIO
  calls, strict HTTP request boundary, conflicting-mode rejection.
  `artifacts/test-results/local-mcp/local-bridge-first.trx`, exit 0.

Additional hostile-response tests produced a real RED: 2 failed, 4 passed, exit 1.
A null evaluation reason caused an unhandled null-reference error, and a review
reference for another application was accepted. The fixes validate nulls and bind
the review reference exactly to `local-application:<applicationRef>`.
The original report remains at
`artifacts/test-results/local-mcp/local-response-red.trx`.

Final commands used the pinned per-user .NET SDK, `--no-restore` and synthetic data:

```powershell
dotnet test tests/JobAgent.Mcp.Tests/JobAgent.Mcp.Tests.csproj --no-restore --logger 'trx;LogFileName=local-mcp-full-green.trx' --results-directory artifacts/test-results/local-mcp --verbosity minimal
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --filter 'FullyQualifiedName~LocalBridgeTests|FullyQualifiedName~HostBridgeBoundaryTests|FullyQualifiedName~HostUiWorkflowTests' --logger 'trx;LogFileName=local-bridge-green.trx' --results-directory artifacts/test-results/local-mcp --verbosity minimal
```

Results: **22/22 MCP tests passed**, and **13/13 selected E2E tests passed**, both
exit 0, no skipped tests, build warnings or build errors. The second run includes
the existing real Chromium synthetic UI approval/upload/submission/verified-receipt
regression. The local twelve-tool sequence left the independent trap site's entire
request count at **0**, preserved the reviewed profile/current job, rejected a stale
answer-proposal revision, kept answer memory unchanged, denied personal execution
and persisted cancellation. Private salary/contact values and tokens were absent
from inspected host outputs. No model invocation, paid API, global host setting
change, commit or public push was performed in this work slice.

Full-release verification, rebuilt package validation and any new actual-model
local-mode scenario remain separate steps. Existing actual model evidence for the
legacy synthetic path is retained in [codex-workflow.md](codex-workflow.md).
