# W02 profile storage migration evidence

Date: 2026-09-19
Scope: local profile SQLite schema upgrades, update audit metadata, and atomic deletion

## Implemented behavior

Profile storage now uses SQLite `PRAGMA user_version` with an explicit, bounded upgrade
chain:

1. **Legacy version 0 → version 1:** an existing database created by the previous
   `EnsureCreated` implementation is validated for the required `ProfileRevisions` and
   `PendingPatches` tables and columns. Existing protected payload rows are not rewritten.
   An empty database receives the equivalent version-1 baseline tables and unique profile
   revision index.
2. **Version 1 → version 2:** `ProfileAuditEntries` is added and the schema version is
   advanced to 2.
3. **Version 2:** required tables and columns are validated on every initialization.

Both upgrade steps, version changes, and final validation run in one SQLite transaction.
The version is read before that transaction or any DDL. A database whose `user_version`
is newer than 2 is rejected without changing its pragma, tables, or rows. A malformed
current schema is also rejected.

New profile, patch proposal, and applied-patch writes add metadata-only audit rows in the
same write transaction. Audit fields are profile ID, profile version, operation,
optional patch correlation ID, and timestamp. The audit schema has no payload/value
column and does not log profile JSON, name, email, salary, or fact values. A supplied
`TimeProvider` makes audit and revision timestamps deterministic in tests; existing
two-argument repository construction remains compatible.

Profile deletion now removes revisions, pending patches, and audit metadata in one
transaction. A test trigger forces the middle delete to fail and verifies that the
earlier revision delete rolls back. A subsequent successful delete removes all three
sets.

Existing DPAPI behavior and protected legacy payload bytes are unchanged. The legacy
upgrade test opens a previous `EnsureCreated` database, initializes the new repository,
and reads the original synthetic profile and version successfully.

## TDD evidence

Raw logs are ignored under `artifacts/storage-migrations/`.

| Command | Exit | Result |
|---|---:|---|
| `dotnet test tests/JobAgent.Infrastructure.Tests/JobAgent.Infrastructure.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~ProfileSchemaMigrationTests"` | 1 | RED: missing `ProfileSchema`, audit set, and deterministic-clock constructor (`red.log`) |
| same filtered command after implementation | 0 | 5 passed, 0 failed, 0 skipped (`green-filtered.log`) |
| full `JobAgent.Infrastructure.Tests` project | 0 | 20 passed, 0 failed, 0 skipped (`green-full.log`) |

The five new tests cover legacy data preservation, future-version fail-closed behavior
without mutation, malformed-current-schema rejection, deterministic metadata-only audit,
and transactional deletion rollback.

## Remaining W02 gaps

- This is an explicit application-managed SQLite upgrade chain, not a claim of a full EF
  Core migrations bundle or general migration framework. Only versions 0, 1, and 2 are
  supported; there is no downgrade path.
- Current-schema validation checks required table and column names. It does not yet
  fingerprint every column type, nullability rule, index, trigger, or constraint.
- Existing legacy revisions are deliberately not backfilled into audit history because
  their original actor, correlation, and event time cannot be reconstructed honestly.
- Audit metadata is local and is deleted with the profile. It is not append-only,
  tamper-evident, cryptographically signed, or an external compliance log; it does not
  add trusted UI/session provenance beyond the existing approval boundary.
- The SQLite file is not whole-database encrypted. Sensitive profile and patch payloads
  remain protected with Windows current-user DPAPI; structural metadata and the bounded
  audit fields remain visible to a user who can read the local database file.
- There is no automatic pre-upgrade backup/restore workflow, cross-machine DPAPI key
  recovery, or retry policy for another process holding a SQLite schema lock.

No Core profile types, document importer, application journal, project files, solution,
or scripts were changed in this slice. No full solution build was run; parent integration
owns the final release build and verification.
