# Local acceptance audit: W00-W12

**Follow-up (2026-09-20):** the three P0 findings below were implemented and the
combined suite passed 255 tests at `artifacts/verification/20260919T230618Z/`.
This audit is retained as the original finding record, not the current feature
matrix. See the latest [verification ledger](../VERIFICATION.md). FR-15/FR-16
remain the next bounded local feature work; publication/package checks are separate.

**Date:** 2026-09-20
**Reviewed checkpoint:** `ac53072`, plus the uncommitted W07 reusable-control work that was in progress during this read-only review.
**Method:** source and evidence review only. No build, test, browser run, package, network call, or external action was performed.

## Acceptance conclusion

The repository already supports a protected personal CV/job review workspace and a separately tested fixed-synthetic browser/application workflow. The planned W07 select, checkbox, radio, and conditional-step work can complete the browser-control slice, but it does not by itself complete the W00-W12 engineering deliverable.

Three local P0 gaps remain before the project can accurately claim the core W00-W12 behavior rather than a partial prototype:

1. an application must persist and display unresolved questions, remember a reviewed answer at the chosen scope, invalidate stale approvals, and resume the same application;
2. the local MCP host must expose the planned general common-service contract rather than six fixed-synthetic/read-only operations;
3. every durable writable store used by the product needs an explicit upgrade/future-version contract, and application transitions need metadata-only audit history. Private-data migration also needs the protected backup/rollback path required by the master plan.

The current evidence supports narrower claims: G1 for the fixed synthetic workflow, protected local profile/job review, deterministic answer and evaluation engines, and a real local STDIO/host demonstration. It does not support “W00-W12 complete” or “general job-application agent complete.”

## Blocking findings

### 1. W08 cannot pause for a new question and resume the same application

**Requirements affected:** user journey B, FR-02, FR-06, W05, W08 `NewQuestion_PausesAndRemembersScopedAnswer`, master sections 7.4 and 8.5.

**Current evidence:**

- `src/JobAgent.Infrastructure/Applications/DemoWorkflow.cs:58-64` defines four fixed questions. `CreateDraftCoreAsync` resolves all four before creating an `ApplicationDraft`; an unresolved answer throws `NeedsInput` at lines 327-330, before the journal write at lines 333-347. There is therefore no persisted application/question snapshot to resume.
- `src/JobAgent.Infrastructure/Workspace/LocalWorkspace.cs:263-300` can resolve a caller-supplied question and save reviewed memory, but it does not own an application record, unresolved-question set, answer package, approval, or resume transition.
- `src/JobAgent.Web/DashboardHost.cs:117-127` exposes the personal workspace and its manual answer-memory endpoints separately from the synthetic application endpoints. The synthetic workflow does not consume the personal workspace's reviewed answer in a resumable application transition.
- `src/JobAgent.Core/Profiles/ProfileModels.cs:7`, `AnswerResolver.cs:137-139`, and `LocalWorkspace.cs:287-292` support only default, company, and exact-application scopes. The master contract also includes role-group scope.

**Small behavior needed:**

- Add a common application service/aggregate that writes an application and its discovered question snapshot before resolution, records the grouped unresolved/review-required questions, and persists `NeedsInput` without opening or advancing the browser.
- Expose those questions in the authenticated local UI. A user-reviewed answer must go through `AnswerMemoryService`, with application, company, role-group, or default scope. Introduce a stable role-group identifier rather than deriving scope from display text at lookup time.
- Re-resolve the stored questions after the memory update, rebuild the answer package/payload hash, clear any prior sharing/submission approval, and resume that same application from the pre-sharing state. Do not silently create a second application.
- Add the named W08 E2E regression: zero POST while a required answer is missing; reviewed memory is reused only in its matching scope; the same application continues after review; changed questions/answers invalidate prior approval.

Likely touch points are `src/JobAgent.Core/Applications/`, `src/JobAgent.Core/Profiles/ProfileModels.cs`, `src/JobAgent.Core/Answers/AnswerResolver.cs`, a common service under `src/JobAgent.Infrastructure/Applications/`, `src/JobAgent.Web/DashboardHost.cs`, `web/src/LocalWorkspace.tsx` or a dedicated application-review component, and focused Core/E2E tests. Extending only `DemoWorkflow` would leave the personal workflow isolated and would not satisfy the common-service requirement.

### 2. W09 implements six operations, not the planned general 12-tool service

**Requirements affected:** FR-11, W09, master sections 11.1-11.2 and 28.1.

**Current evidence:**

- `src/JobAgent.Mcp/ReadOnlyTools.cs:39-75` exposes `runtime_get_capabilities`, `profile_get_summary`, and `application_get_status`.
- `src/JobAgent.Mcp/SyntheticCommandTools.cs:11-23` conditionally exposes `application_create_draft`, `application_prepare_review`, and `application_execute_approved`, all bound to the fixed synthetic companion workflow.
- `src/JobAgent.Mcp/Program.cs:27-31` validates arguments for only those six operations. The create command accepts no job/profile/resume references, so it is not the general section 11.1 contract despite sharing the planned name.
- `docs/THREAT_MODEL.md:23` and `docs/PROJECT_STATE.md:17` accurately disclose this limit.

**Small behavior needed:**

Bind the MCP layer to the same common application/workspace services used by the local UI and add the missing proposal/read operations:

- `profile_propose_patch`: create a pending version-bound proposal only; never verify or apply it.
- `job_import_text`: accept bounded provided text and optional metadata; never fetch `sourceUrl`.
- `job_evaluate`: return the existing typed assessments and unknowns without private salary minimum.
- `application_get_questions`: return the persisted unresolved/review-required question set from finding 1.
- `application_propose_answers`: schema/evidence-validate proposals and leave them unverified/review-required.
- `application_cancel`: atomically prevent future browser work for the referenced application.

Generalize `application_create_draft`, `application_prepare_review`, `application_execute_approved`, and `application_get_status` to canonical local references while preserving the existing security boundary: there must still be no approval-minting tool; preparation only creates a local review reference; execution only consumes a valid UI-stored receipt. Keep the fixed synthetic commands as a clearly named fixture adapter or implement them through the general service with fixed fixture references.

Contract tests should enumerate all 12 tools, reject unknown/oversized fields, exercise optimistic concurrency, assert that no tool can mint approval, and run an actual STDIO sequence over the general service. A remote MCP transport, public hosted store, and live employer adapter are not required for this local W09 acceptance.

### 3. Durable schema recovery and application audit coverage are incomplete

**Requirements affected:** FR-02, W02, W06 audit/transaction requirement, W11 export/delete recovery boundary, and master section 16.3.

**Current evidence:**

- The profile repository has the strongest implementation: `src/JobAgent.Infrastructure/Storage/ProfileSchema.cs` has explicit v0/v1/v2 handling, `PRAGMA user_version`, a future-version failure, transactional migration, and `ProfileAuditEntries`. This satisfies the bounded profile migration foundation.
- `docs/evidence/storage-migrations.md` correctly records the remaining profile-store gap: there is no automatic protected pre-upgrade backup and rollback path.
- The private personal workspace still executes `PRAGMA secure_delete=ON; CREATE TABLE IF NOT EXISTS Workspace ...` on every connection (`src/JobAgent.Infrastructure/Workspace/LocalWorkspace.cs:346-353`). It has no schema version, future-version rejection, legacy migration test, protected pre-migration backup, rollback path, or metadata-only operation audit. This is the highest-priority store because it contains the user's protected original CV, profile history, answers, and reviewed job.
- `src/JobAgent.Infrastructure/Jobs/JobRepository.cs:50-56` uses `EnsureCreatedAsync`. It protects payloads, but has no explicit upgrade/future-version contract or operation audit.
- `src/JobAgent.Infrastructure/Applications/ApplicationJournal.cs:15-20` also uses `EnsureCreatedAsync`. Updates overwrite the current serialized body and increment a revision (`lines 49-61`); there is no append-only metadata record of state transitions. This journal is currently synthetic-only (`lines 21-25`), which limits privacy impact, but a general application service cannot inherit that schema unchanged.

**Small behavior needed:**

- Give `personal/workspace.db`, `jobs.db`, and the application journal an application-owned `user_version` initializer. It must read and reject a newer version before DDL or mutable pragmas, migrate supported old fixtures in one transaction, preserve encrypted payload bytes/rows, and test restart and rollback after an injected migration failure.
- Before a private profile/workspace schema mutation, create a protected local backup outside the checkout and expose a tested restore path. Record only safe backup metadata, not decrypted payloads.
- Add metadata-only audit rows in the same transaction as personal-workspace/job mutations and application state transitions: entity reference, previous/new revision or state, operation, trusted actor/source, correlation ID, and timestamp. Do not copy CV text, answers, salary, document bytes, or serialized application bodies into audit rows.
- Keep the synthetic journal's plaintext limitation explicit until a protected general application store replaces or upgrades it.

This is required before future schema changes or general personal application persistence can be called recoverable. A downgrade mechanism is optional; fail-closed newer-version detection plus protected backup/restore is the relevant acceptance boundary.

## Explicit requirements that are not part of the three P0 blockers

Two feasible P1 requirements remain unimplemented and must stay marked as deferred if the core P0 release proceeds:

- **FR-15 CV/cover-letter adaptation and diff review:** there is no grounded adaptation proposal or document diff workflow. A bounded implementation would add an evidence-linked proposal type under `src/JobAgent.Core/Documents/`, a local side-by-side review endpoint/component, and tests proving that unsupported claims cannot enter an approved document. This is not externally blocked, but section 28.1 does not make it a condition of the initial core synthetic delivery.
- **FR-16 runtime provider/cost-limit selection:** the evaluation reports identify model and prompt versions, and ordinary operation needs no paid API, but there is no runtime provider/call-budget policy. A bounded implementation would add a typed execution policy under `src/JobAgent.Core/Models/` and reject calls beyond an explicit per-run budget. This is not needed for Fixture/HostMediated operation while paid API use remains disabled.

W12 also needs fresh evidence after the W07 and subsequent fixes: the current 188-test report predates those changes, and the documented clean-checkout run reused the current Windows user's dependency/browser caches rather than proving a fresh user profile (`docs/VERIFICATION.md:60-67`). This is a release verification gate, not another product feature. The exact release commit should receive the locked restore/build/test/package/doctor checks and a clean-profile or honestly scoped installation result.

## Justified exclusions and external gates

- FR-13 live-source automation and FR-14 LinkedIn automation remain correctly `BlockedExternal`: there is no verified target/platform authorization. The paste-only job path and `LinkedInRestricted` behavior are the correct local result. Missing authorization is not a reason to weaken the permission checks or a blocker to the synthetic/local core.
- Real employer submission remains gated on a concrete selected application package and action-time user approval. The authorized public repository and OSS submission do not authorize job applications.
- CAPTCHA/MFA solving is intentionally excluded. The fail-closed `ManualTakeoverRequired` behavior is correct. Same-page interactive takeover/resume is a documented usability limit, not required to claim the tested stop behavior.
- OCR, remote MCP hosting, a public multi-user data service, full desktop control, paid API use, independent adoption, and program/store acceptance are outside this W00-W12 local core claim. Their absence must remain visible, but they should not be presented as local coding blockers.

## Top three bounded next tasks

1. **Implement the resumable-question slice:** persist discovered questions and `NeedsInput`, connect the authenticated review UI to scoped memory including role-group scope, invalidate old approvals, and pass `NewQuestion_PausesAndRemembersScopedAnswer` against the same application ID.
2. **Complete the local 12-tool MCP contract:** bind all tools to the common services, add the six missing operations, generalize the four application operations that are currently fixture-bound, and preserve the UI-only approval boundary with protocol/STDIO tests.
3. **Finish durable-store recovery and audit contracts:** start with the private personal workspace, then the job/application stores; add fail-closed versioned migrations, protected pre-migration backup/restore, and transactional metadata-only mutation/state-transition audits.
