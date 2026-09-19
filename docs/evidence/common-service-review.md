# Common application service review fixes

**Date:** 2026-09-20  
**Scope:** evidence validity, answer-memory scope identity, pending proposal freshness and lifecycle, UI cancellation concurrency, and malformed local request boundaries. No browser submission, live employer adapter, external request, commit, or push was part of this slice.

## Test-first record

The focused Core RED run completed with 10 passing and 2 failing tests:

```text
dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~AnswerProposalTests|FullyQualifiedName~ApplicationQuestionTests"
exit 1 — 10 passed, 2 failed
artifacts/test-results/common-service-core-red/common-service-core-red.trx
```

The failures demonstrated both intended defects: evidence whose `ValidUntil` equaled the evaluation instant was accepted as grounded, and a null question element raised `NullReferenceException` instead of a bounded argument error.

The Workspace RED compile failed because the new `DiscardProposalAsync` and revision-bound `CancelApplicationFromUiAsync` APIs did not exist. It also exposed a missing test-only namespace import, which was corrected before judging product behavior:

```text
dotnet test tests/JobAgent.Workspace.Tests/JobAgent.Workspace.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~WorkspaceTests|FullyQualifiedName~WorkspaceApplicationReviewTests"
exit 1
```

## Implemented behavior

- Verified evidence expires when `ValidUntil <= now`; proposal validation and scoped memory resolution share that rule.
- The legacy answer-memory endpoint rejects Application scope instead of using a job ID as an application identity. Its resolver supplies an empty application context so legacy reads can use Default, Company, or RoleGroup memory without treating job IDs as application IDs. The actual application review path continues to bind Application scope to the draft GUID.
- Pending answer proposals record the profile version that grounded them. Views drop proposals when profile version, resume/job source, or any cited verified evidence is no longer current. Accepting a reviewed answer removes its proposal case-insensitively.
- Exact pending job retries are idempotent after URL canonicalization and do not increment the workspace revision. Same-employer postings with a different title, canonical URL, or text hash remain distinct.
- `DiscardProposalAsync(Guid proposalRef, long expectedRevision)` removes exactly one pending profile or job proposal under optimistic concurrency.
- Host cancellation remains the idempotent emergency-stop contract. UI cancellation uses `CancelApplicationFromUiAsync(Guid applicationRef, long expectedRevision)`, and the route requires a revision body so a stale tab cannot cancel a newer draft.
- Null question elements, reviewed answers, source URLs, requirements collections, and requirement elements are rejected through bounded argument errors rather than null dereferences.

## Focused GREEN

```text
dotnet test tests/JobAgent.Core.Tests/JobAgent.Core.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~AnswerProposalTests|FullyQualifiedName~ApplicationQuestionTests"
exit 0 — 12 passed, 0 failed, 0 skipped
artifacts/test-results/common-service-core-green/common-service-core-green.trx

dotnet test tests/JobAgent.Workspace.Tests/JobAgent.Workspace.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~WorkspaceTests|FullyQualifiedName~WorkspaceApplicationReviewTests"
exit 0 — 17 passed, 0 failed, 0 skipped
artifacts/test-results/common-service-workspace-green/common-service-workspace-green.trx
```

The first Workspace GREEN compile found one missing product namespace import for `PolicyException`; the same focused command passed after that mechanical correction. Final verification is limited to these focused suites and the parent-owned integration run; this document does not claim a full solution or live-model/browser result.

After terminal-state proposal filtering and legacy null guards were tightened, both focused suites were rerun from the final source state with the same results. Their final reports are:

```text
artifacts/test-results/common-service-final/common-service-core-final.trx — 12 passed
artifacts/test-results/common-service-final/common-service-workspace-final.trx — 17 passed

dotnet build src/JobAgent.Web/JobAgent.Web.csproj --no-restore
exit 0 — 0 warnings, 0 errors
```

The Web build verifies the revision-bearing cancellation endpoint and proposal-discard endpoint compile against the final service API.

## Boundaries

- Pending answer proposals remain unapproved suggestions. Citation membership establishes that evidence was supplied and current, not that the proposed prose is entailed by it.
- The personal execution method remains unconditionally blocked by `BlockedPermission`; these changes add no employer submission permission.
- Proposal discard is local UI state management. It does not delete the reviewed profile, accepted job, source document, or application draft.
- Existing incorrectly keyed legacy Application-scope memories are intentionally no longer resolved through the generic preview endpoint. Users must review the answer in the actual application flow to create a draft-GUID-scoped memory.
