# W01/W06 bounded code review

**Reviewed:** 2026-09-18 03:07 +03:00
**Base commit:** `8686011` plus the current working tree
**Scope:** `src/JobAgent.Core/Applications`, `src/JobAgent.Infrastructure/Applications`, `sandbox/JobAgent.FakeCareerSite`, and the W01/W06 tests in `tests/JobAgent.E2E.Tests`. Compared with master-plan sections 8, 15, W01, and W06. Foundation/profile/browser work outside this scope was not assessed.

## Findings

### P1 — A caller can fabricate a valid approval receipt

`ApprovalReceipt` has a public value constructor, while `ApprovalPolicy.Validate` proves only that caller-supplied fields match the draft. It does not prove that the receipt was issued by a trusted UI or loaded from a trusted approval store. `CreateAsync` also accepts a `WorkflowRecord` containing any such receipt. The method name `GrantFromUserInterface` is not a trust boundary.

Locations: `src/JobAgent.Core/Applications/ApplicationDraft.cs:38`, `src/JobAgent.Core/Applications/ApprovalPolicy.cs:10`, `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:20`.

Reproduction:

```csharp
var draft = ReadyDraft();
var forged = new ApprovalReceipt(
    Guid.NewGuid(), draft.Id, draft.PayloadHash(), draft.RecipientOrigin,
    draft.ProfileVersion, draft.ResumeHash, "any-non-empty-session",
    ApprovalPurpose.Submit, now, now.AddYears(10));

ApprovalPolicy.Validate(draft, forged, ApprovalPurpose.Submit, now); // succeeds
await journal.CreateAsync(new WorkflowRecord(draft, Submission: forged));
await journal.ClaimSubmissionAsync(draft.Id, now);                  // succeeds
```

Impact: a model-facing/application caller that can construct or deserialize a workflow record can mint its own approval, choose an arbitrary validity period, and pass the submission claim. This violates sections 8.2–8.3 and W06's “model only executes the approved package” acceptance criterion.

Fix target: make approval issuance a trusted persistence operation. Accept an approval ID at execution time, load the immutable UI-issued record from the local store, and atomically validate/consume it. Do not accept a complete `ApprovalReceipt` supplied by a model/tool boundary. Enforce the ten-minute maximum at issuance and validation.

### P1 — Public generic updates bypass the state machine, approval, and evidence requirements

`ApplicationJournal.UpdateAsync` checks the expected current state and optimistic revision, but accepts every next state returned by an arbitrary delegate. There is no allowed-transition graph and no requirement for approval or receipt evidence on terminal states. The enum is also missing section 8 states such as `Discovered`, `Evaluated`, `Selected`, `ReadyForReview`, `ManualStep`, and `ReportedByUser`.

Locations: `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:45`, `src/JobAgent.Core/Applications/ApplicationDraft.cs:7`.

Reproduction:

```csharp
await journal.UpdateAsync(id, ApplicationStatus.AwaitingSubmissionApproval, current =>
    current with
    {
        Draft = current.Draft with { Status = ApplicationStatus.SubmittedVerified },
        Submission = null,
        Evidence = null
    }); // succeeds
```

The same API permits backwards or forbidden edges such as `SubmittedVerified → Filling` when the caller supplies the observed current state.

Impact: callers can skip `ClaimSubmissionAsync`, mark an application submitted without approval/evidence, or reopen terminal applications. Optimistic concurrency prevents lost updates but does not enforce workflow correctness.

Fix target: make the raw compare-and-swap update private/internal and expose named transition methods backed by one explicit allowed-edge table. Require a consumed matching approval for `AwaitingSubmissionApproval → Submitting`, verified receipt evidence for `Submitting → SubmittedVerified`, and a recorded uncertain attempt for `Submitting → SubmittedUnverified`. Add the section 8 states or document an explicit, approved scope reduction.

### P1 — Uncertain recovery deletes the only approval/attempt audit record

The claim atomically marks the stored receipt used, but `RecoverInterruptedAsync` then sets both approval fields to `null`. No `SubmissionAttempt` type or append-only attempt row records the approval ID, payload/CV/profile hashes, start time, idempotency key, or outcome. After a crash, `SubmittedUnverified` correctly blocks a blind retry, but the journal can no longer show which user approval authorized the attempt or when it began.

Locations: `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:59`, `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:76`, `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:86`; required metadata is described in master section 8.1 and W06.

Reproduction:

```csharp
await journal.CreateAsync(readyWithApproval);
await journal.ClaimSubmissionAsync(id, now);
await journal.RecoverInterruptedAsync();
var recovered = await journal.GetAsync(id);

Assert.Equal(ApplicationStatus.SubmittedUnverified, recovered!.Draft.Status);
Assert.Null(recovered.Submission); // consumed approval and its ID are gone
```

Impact: uncertain submissions cannot be reconciled against immutable authorization/attempt metadata, and an audit cannot distinguish multiple historical attempts. This falls short of section 8.1's `SubmissionAttempt` record and section 8.4's evidence requirements.

Fix target: create an immutable `SubmissionAttempt` in the same atomic claim that consumes approval and enters `Submitting`. Preserve approval ID, application/job ID, profile version, payload/answer/CV hashes, recipient, idempotency key, and start time. Recovery should set that attempt's result to unknown and retain it; later receipt reconciliation should append/attach evidence rather than overwrite history.

### P2 — A restart incorrectly fails applications that were only waiting for approval

`RecoverInterruptedAsync` classifies `AwaitingSubmissionApproval` as an interrupted browser operation, moves it to `FailedBeforeSubmission`, and deletes any pending approval. Merely restarting while waiting for the user's decision is not evidence of a lost browser session or failed submission.

Location: `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:81`.

Reproduction:

```csharp
await journal.CreateAsync(readyAwaitingApproval); // no claim, no external action
await journal.RecoverInterruptedAsync();
var recovered = await journal.GetAsync(id);

Assert.Equal(ApplicationStatus.FailedBeforeSubmission, recovered!.Draft.Status);
Assert.Equal("BrowserSessionLost", recovered.Error);
```

Impact: safe pending work becomes a failure after every process restart. If the user approved shortly before restart, that approval is silently discarded even though no external action started.

Fix target: leave `AwaitingSubmissionApproval` unchanged. Recover only states that own a persisted live browser/attempt lease; represent browser-session loss independently from a durable approval-wait state.

### P2 — The receipt's user-session field is recorded but never bound to the executing session

`Validate` checks only that `UserSessionId` is non-empty. It has no current session parameter and never compares the receipt's session with the actor/session requesting the claim.

Location: `src/JobAgent.Core/Applications/ApprovalPolicy.cs:10`.

Reproduction: issue a receipt with `UserSessionId = "session-A"`, then execute the same `Validate`/`ClaimSubmissionAsync` path while handling session B. There is no API argument through which session B can be rejected, so the claim succeeds.

Impact: a valid bearer receipt copied between local sessions can be consumed by the wrong session. The stored field gives an audit impression of binding without enforcing it.

Fix target: pass a trusted current-session/user identity into claim and compare it with the persisted issuer/session, or deliberately remove session binding from the schema and document a different trusted actor model. Do not derive the current identity from model input.

### P2 — Approval validation uses a terminal-state blacklist instead of purpose-specific allowed states

`ApprovalPolicy.Validate` rejects only cancelled, submitting, and submitted drafts. A submit approval therefore validates in `Drafting`, `NeedsInput`, `ReadyForDataSharing`, `Filling`, `FailedBeforeSubmission`, and `BlockedPermission`. `GrantFromUserInterface` likewise issues a submit receipt in any state. Because `Status` is intentionally absent from `PayloadHash`, a receipt minted early can remain valid after the draft is moved to `AwaitingSubmissionApproval`.

Locations: `src/JobAgent.Core/Applications/ApprovalPolicy.cs:5`, `src/JobAgent.Core/Applications/ApprovalPolicy.cs:23`, `src/JobAgent.Core/Applications/ApplicationDraft.cs:30`.

Reproduction:

```csharp
var drafting = draft with { Status = ApplicationStatus.Drafting };
var receipt = ApprovalPolicy.GrantFromUserInterface(
    drafting, ApprovalPurpose.Submit, "session", now);
ApprovalPolicy.Validate(drafting, receipt, ApprovalPurpose.Submit, now); // succeeds
```

Impact: the domain policy itself accepts approval before the final review state and while a permission block is active. `ClaimSubmissionAsync` happens to require the later state, but that does not make the original approval action-time or final-package review.

Fix target: allow `Submit` issuance/validation only in `AwaitingSubmissionApproval`, and `ShareData` only in its explicitly chosen pre-fill state. Add tests for every disallowed state, especially `BlockedPermission`, `NeedsInput`, and `FailedBeforeSubmission`.

### P2 — Claim failures erase the reason needed for safe recovery

`ClaimSubmissionAsync` catches every `PolicyException` and returns `false`. Missing application, expired approval, changed package, already-used approval, invalid state, and genuine concurrent loss are indistinguishable.

Location: `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:74`.

Reproduction: call `ClaimSubmissionAsync` once with an expired receipt and once from the losing concurrent claimant; both return only `false`.

Impact: the caller cannot tell whether to request fresh approval, refresh state, show a package-change warning, or investigate missing/corrupt data. Treating every false as “another worker claimed it” could hide a security-relevant mismatch.

Fix target: return a typed result (`Claimed`, `ConcurrentClaim`, `Expired`, `PackageChanged`, `MissingApproval`, `InvalidState`, `NotFound`) or catch only `ConcurrentChange` and surface the other policy codes.

## Confirmed behavior and coverage notes

- The SQLite `Revision` predicate and single `ExecuteUpdateAsync` make two claimers race on the same revision; only one can commit. The existing concurrent-claim test covers the main double-claim case.
- A recovered `Submitting` record becomes `SubmittedUnverified`, and a later claim is rejected. This is the correct conservative direction for an unknown post-submit result; the missing immutable attempt record is the defect described above.
- W01's fake site binds to caller-provided loopback URLs, exposes health, displays the synthetic warning, validates fixed synthetic identity and resume content, and tests random ports with disposable hosts/browser instances.
- `FakeCareerSite_ReceivesOnlySyntheticApplication` exercises only the accepted synthetic path. Add a negative HTTP/browser case with a non-synthetic name, flag, or resume and assert `400` plus zero receipts so the “only synthetic” invariant cannot regress unnoticed.
- `ReceiptStore.GetOrAdd` silently returns an earlier receipt when the same application key is replayed with different answers or resume content. If this fixture is meant to test idempotency, add a test and return a conflict for a same-key/different-payload replay; otherwise the fake site can hide payload-drift bugs.

## Verification

The parent task reported 17 passing tests. An independent rerun in this review process was attempted with:

```text
dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --verbosity minimal
```

It did not start because `global.json` pins SDK `10.0.401` with `rollForward: disable`, while this process currently sees only SDK `8.0.319` and `9.0.103`. No test result is claimed from this review run. The findings above are direct, deterministic paths through the reviewed APIs; each reproduction should be added as a regression test after the required SDK is available.
