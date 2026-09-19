using System.Data;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Workspace;

internal static class WorkspaceSchema
{
    public const int CurrentVersion = 1;

    public static async Task InitializeAsync(SqliteConnection connection, string databasePath,
        IPayloadProtector protector, CancellationToken cancellationToken)
    {
        var observedVersion = await ReadVersionAsync(connection, null, cancellationToken);
        if (observedVersion > CurrentVersion)
            throw new InvalidDataException(
                $"Workspace schema version {observedVersion} is newer than supported version {CurrentVersion}.");

        var tableCount = await UserTableCountAsync(connection, null, cancellationToken);
        if (observedVersion == 0 && tableCount != 0)
            await ProtectedDatabaseRecovery.CreateIfMissingAsync(connection, databasePath,
                "workspace", observedVersion, protector, cancellationToken);

        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var version = await ReadVersionAsync(connection, transaction, cancellationToken);
        if (version > CurrentVersion)
            throw new InvalidDataException(
                $"Workspace schema version {version} is newer than supported version {CurrentVersion}.");
        if (version == 0)
        {
            if (tableCount == 0)
                await ExecuteAsync(connection, transaction, """
                    CREATE TABLE Workspace (Id INTEGER PRIMARY KEY CHECK(Id=1), Revision INTEGER NOT NULL, Payload BLOB NOT NULL);
                    """, cancellationToken);
            else
                await ValidateTableAsync(connection, transaction, "Workspace",
                    ["Id", "Revision", "Payload"], cancellationToken);
            await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS WorkspaceAuditEntries (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    EntityRef TEXT NOT NULL,
                    PreviousRevision INTEGER NOT NULL,
                    NewRevision INTEGER NOT NULL,
                    Operation TEXT NOT NULL,
                    Actor TEXT NOT NULL,
                    CorrelationId TEXT NULL,
                    OccurredAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_WorkspaceAuditEntries_NewRevision
                    ON WorkspaceAuditEntries(NewRevision);
                PRAGMA user_version=1;
                """, cancellationToken);
            version = 1;
        }
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported workspace schema version {version}.");
        await ValidateTableAsync(connection, transaction, "Workspace",
            ["Id", "Revision", "Payload"], cancellationToken);
        await ValidateTableAsync(connection, transaction, "WorkspaceAuditEntries",
            ["Id", "EntityRef", "PreviousRevision", "NewRevision", "Operation", "Actor", "CorrelationId", "OccurredAt"],
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ValidateTableAsync(SqliteConnection connection, SqliteTransaction transaction,
        string table, IReadOnlyList<string> expectedColumns, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(1));
        var missing = expectedColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException(
                $"Workspace schema table {table} is missing required columns: {string.Join(", ", missing)}.");
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
