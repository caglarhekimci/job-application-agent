# Durable-store recovery and audit evidence

**Date:** 2026-09-20  
**Scope:** existing profile, personal workspace, reviewed-job, and synthetic application-journal schemas. No UI, browser, MCP, live employer, external network, commit, or push operation was part of this slice.

## Test-first record

Focused RED was run before the schema/recovery implementation:

```text
dotnet test tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~StoreRecoveryTests
exit 1
```

The corrected test source failed only because `ProfileRepository.RestoreLatestBackupAsync`, `JobRepository.RestoreLatestBackupAsync`, and the time-provider-aware `ApplicationJournal` constructor did not exist.

```text
dotnet test tests/JobAgent.Workspace.Tests/JobAgent.Workspace.Tests.csproj --no-restore --filter FullyQualifiedName~WorkspaceStoreRecoveryTests
exit 1
```

It failed only because `LocalWorkspace.RestoreLatestBackupAsync` and the time-provider constructor argument did not exist. The first Infrastructure RED attempt also exposed and corrected a test-only `Dictionary`/`SortedDictionary` type mismatch before the recorded feature RED.

Focused GREEN:

```text
dotnet test tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj --no-restore --filter FullyQualifiedName~StoreRecoveryTests
exit 0 — 8 passed, 0 failed, 0 skipped
artifacts/test-results/store-recovery-green/store-recovery-green.trx

dotnet test tests/JobAgent.Workspace.Tests/JobAgent.Workspace.Tests.csproj --no-restore --filter FullyQualifiedName~WorkspaceStoreRecoveryTests
exit 0 — 5 passed, 0 failed, 0 skipped
artifacts/test-results/workspace-recovery-green/workspace-recovery-green.trx
```

An attempted parallel GREEN rerun caused one compiler `CS2012` shared-output lock while the other project passed. It was a test-orchestration collision, not a product failure; the affected Infrastructure command was rerun alone and passed 8/8 as recorded above.

Deletion-backup privacy was then tested separately. Before invalidation, each test exited 1 because the protected backup still existed after live-store deletion:

```text
ProfileDeletion_InvalidatesWholeStoreRecoverySnapshots
exit 1 — 0 passed, 1 failed
artifacts/test-results/backup-delete-red/backup-delete-red.trx

WorkspaceDeletion_InvalidatesItsRecoverySnapshot
exit 1 — 0 passed, 1 failed
artifacts/test-results/workspace-backup-delete-red/workspace-backup-delete-red.trx
```

After exact derived-path invalidation was added ahead of the live deletion, both commands exited 0 with 1/1 passed:

```text
artifacts/test-results/backup-delete-green/backup-delete-green.trx
artifacts/test-results/workspace-backup-delete-green/workspace-backup-delete-green.trx
```

The final neighboring regression run was sequential and completed without failures:

```text
dotnet test tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj --no-restore
exit 0 — 41 passed, 0 failed, 0 skipped
artifacts/test-results/store-recovery-regression/store-recovery-infrastructure.trx

dotnet test tests/JobAgent.Workspace.Tests/JobAgent.Workspace.Tests.csproj --no-restore
exit 0 — 22 passed, 0 failed, 0 skipped
artifacts/test-results/store-recovery-regression/store-recovery-workspace.trx

dotnet test tests/JobAgent.E2E.Tests/JobAgent.E2E.Tests.csproj --no-restore --filter FullyQualifiedName~ApplicationPersistenceTests
exit 0 — 5 passed, 0 failed, 0 skipped
artifacts/test-results/store-recovery-regression/store-recovery-journal.trx
```

This is 68 passing tests across the three final commands. It is focused and neighboring-regression evidence, not a full-solution verification claim.

## Behaviors exercised

- Profile v0 data upgrades to the existing v2 schema with an opaque protected pre-upgrade snapshot. Explicit restore recovers the original protected row, after which normal initialization upgrades it again.
- A protected backup is bound to its store kind and source schema. A profile backup copied to the derived jobs backup path is rejected without replacing the jobs database.
- Legacy workspace and job payload bytes remain byte-for-byte unchanged through schema migration.
- Repeated failed workspace migration retains the first valid protected backup; the schema version and original payload remain unchanged.
- Workspace, job, and application audit insertion failures roll back the payload/state revision in the same transaction.
- Audit schemas and inspected values contain revision/state metadata, not payload/body columns or fixture PII.
- Profile/workspace deletion invalidates their derived recovery snapshots so deleted private data is not silently retained in a backup.
- Workspace, job, and application future schema versions fail before creating product tables or pre-upgrade backups.
- The synthetic journal's legacy serialized body survives migration unchanged; a claimed submission writes a separate metadata-only state-transition record.

## Implementation boundary

- `ProfileSchema` remains version 2. Backups are added before its existing v0/v1 upgrade paths; no artificial profile schema bump was introduced.
- `WorkspaceSchema`, `JobSchema`, and `ApplicationJournalSchema` are version 1 and adopt the existing `user_version=0` `EnsureCreated` layouts transactionally.
- Job rows gain a numeric revision so the protected payload update and its audit entry can describe the same committed revision.
- Backup creation uses SQLite `BackupDatabase`, then protects a store/schema/hash-bound envelope. Valid version-specific backups are not overwritten.
- Restore paths are derived from constructor-validated database paths. Restore is explicit, checks exclusive access and SQLite integrity, and never silently follows a caller-supplied path.
- The application journal remains synthetic-only. Its schema and transition audit are durable, but its serialized body is intentionally not represented as suitable for personal data.

## Remaining limits

- The focused tests use temporary synthetic stores and a deterministic test protector. Production confidentiality relies on Windows current-user DPAPI and was already covered by the existing payload-protection tests.
- There is no downgrade, remote backup, key export, dashboard restore flow, or recovery from loss of the Windows account/key material.
- Initializers assume the product's normal single-runtime ownership. SQLite supplies transactional DDL/state updates; this slice does not add a cross-process migration coordinator beyond existing runtime ownership and database locking.
