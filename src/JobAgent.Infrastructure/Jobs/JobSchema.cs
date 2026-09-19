using System.Data;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Jobs;

internal static class JobSchema
{
    public const int CurrentVersion = 1;

    public static async Task InitializeAsync(string databasePath, IPayloadProtector protector,
        CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var observedVersion = await ReadVersionAsync(connection, null, cancellationToken);
        if (observedVersion > CurrentVersion)
            throw new InvalidDataException(
                $"Job schema version {observedVersion} is newer than supported version {CurrentVersion}.");
        var tableCount = await UserTableCountAsync(connection, null, cancellationToken);
        if (observedVersion == 0 && tableCount != 0)
            await ProtectedDatabaseRecovery.CreateIfMissingAsync(connection, databasePath,
                "jobs", observedVersion, protector, cancellationToken);

        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var version = await ReadVersionAsync(connection, transaction, cancellationToken);
        if (version == 0)
        {
            if (tableCount == 0)
                await ExecuteAsync(connection, transaction, """
                    CREATE TABLE Jobs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IdentityHash TEXT NOT NULL,
                        Payload BLOB NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        Revision INTEGER NOT NULL
                    );
                    CREATE UNIQUE INDEX IX_Jobs_IdentityHash ON Jobs(IdentityHash);
                    """, cancellationToken);
            else
            {
                await ValidateTableAsync(connection, transaction, "Jobs",
                    ["Id", "IdentityHash", "Payload", "CreatedAt", "UpdatedAt"], cancellationToken);
                await ExecuteAsync(connection, transaction,
                    "ALTER TABLE Jobs ADD COLUMN Revision INTEGER NOT NULL DEFAULT 1;", cancellationToken);
            }
            await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS JobAuditEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EntityRef TEXT NOT NULL,
                    PreviousRevision INTEGER NOT NULL,
                    NewRevision INTEGER NOT NULL,
                    Operation TEXT NOT NULL,
                    Actor TEXT NOT NULL,
                    CorrelationId TEXT NULL,
                    OccurredAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_JobAuditEntries_EntityRef_NewRevision
                    ON JobAuditEntries(EntityRef, NewRevision);
                PRAGMA user_version=1;
                """, cancellationToken);
            version = 1;
        }
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported job schema version {version}.");
        await ValidateTableAsync(connection, transaction, "Jobs",
            ["Id", "IdentityHash", "Payload", "CreatedAt", "UpdatedAt", "Revision"], cancellationToken);
        await ValidateTableAsync(connection, transaction, "JobAuditEntries",
            ["Id", "EntityRef", "PreviousRevision", "NewRevision", "Operation", "Actor", "CorrelationId", "OccurredAt"],
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ValidateTableAsync(SqliteConnection connection, SqliteTransaction transaction,
        string table, IReadOnlyList<string> requiredColumns, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(1));
        var missing = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException(
                $"Job schema table {table} is missing required columns: {string.Join(", ", missing)}.");
    }

    private static async Task<long> UserTableCountAsync(SqliteConnection connection,
        SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> ReadVersionAsync(SqliteConnection connection,
        SqliteTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction,
        string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
