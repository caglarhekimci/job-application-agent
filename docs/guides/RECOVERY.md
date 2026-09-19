# Local store recovery

The profile, personal workspace, and reviewed-job stores create a protected SQLite snapshot immediately before an existing older schema is upgraded. The application journal is currently synthetic-only; it has a transactional schema upgrade but no private-data backup contract.

## Backup files

A backup path is derived from the configured database path. Callers cannot supply a source or destination path:

```text
profiles.db.preupgrade-v0.protected
profiles.db.preupgrade-v1.protected
workspace.db.preupgrade-v0.protected
jobs.db.preupgrade-v0.protected
```

The backup contains a SQLite-consistent snapshot produced with SQLite's backup API. The complete snapshot is placed in an envelope with a format version, store kind, source schema version, and SHA-256 content hash, then passed through the store's existing payload protector. On supported personal Windows use this is current-user DPAPI. Synthetic plaintext protection is permitted only in explicitly enabled non-Windows tests and does not make a confidential backup.

An existing valid backup for the same store kind and source version is retained. A repeated failed migration does not overwrite it. A mismatched, corrupt, or unreadable existing backup stops migration. A database with a newer schema version is rejected before a backup, DDL, or mutable pragma is attempted.

## Explicit restore

Restore is deliberately not automatic. A failed migration leaves the database and its backup for diagnosis.

1. Close the Job Application Agent and every process using the database.
2. Retain a separate copy of the current database and protected backup before recovery work.
3. Ensure no `-wal`, `-shm`, or rollback `-journal` file is present. The restore API rejects this ambiguous state instead of guessing whether journal data should be replayed.
4. Invoke the store-owned method; it selects only the derived backup paths for that configured store:

```csharp
await profileRepository.RestoreLatestBackupAsync();
await jobRepository.RestoreLatestBackupAsync();
await localWorkspace.RestoreLatestBackupAsync();
```

5. Start the store normally. Initialization validates and upgrades the restored older snapshot again. Inspect the error and keep the backup if that upgrade still fails.

Before replacement, restore validates the protected envelope, store kind, schema version, content hash, exclusive access to the target, and SQLite `quick_check`. The replacement is written to the same directory and moved over the database only after validation. The API never accepts an arbitrary restore path.

## Audit and privacy boundary

Workspace, job, and application mutations write their audit row in the same transaction as the new revision or state. Audit tables contain opaque entity references, previous/new revision or state, operation, fixed actor/source, optional correlation ID, and timestamp. They do not contain protected payloads, CV text or bytes, answers, salary values, job text, or serialized application bodies.

The protected backup still represents private user data. Keep it outside the checkout with the database and do not attach it to issues or commits. A profile deletion invalidates every pre-upgrade snapshot for that profile database before deleting the live rows, because a full-database snapshot cannot be edited safely for one profile. A workspace deletion similarly invalidates its snapshot before replacing the protected workspace payload. This can shorten the recovery window for other profiles sharing the same profile database; export or confirm the live store before requesting deletion.

## Boundaries

- There is no downgrade path. Newer schema versions fail closed.
- Recovery is an offline local-maintainer operation in this slice; there is no dashboard or CLI restore button.
- Restore rejects WAL/SHM/rollback-journal ambiguity rather than attempting journal repair.
- The application journal remains limited to synthetic records and plaintext serialized bodies. It must not be reused for personal applications until its payload is protected.
- A backup proves recoverability of the local SQLite snapshot, not recovery from OS-account loss. Current-user DPAPI material is tied to that Windows user.
