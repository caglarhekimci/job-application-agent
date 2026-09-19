using System.Data;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Storage;

public static class ProfileSchema
{
    public const int CurrentVersion = 2;

    internal static async Task InitializeAsync(string databasePath, IPayloadProtector protector,
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
                $"Profile schema version {observedVersion} is newer than supported version {CurrentVersion}.");

        if (observedVersion < CurrentVersion &&
            await UserTableCountAsync(connection, null, cancellationToken) != 0)
            await ProtectedDatabaseRecovery.CreateIfMissingAsync(connection, databasePath,
                "profile", observedVersion, protector, cancellationToken);

        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var version = await ReadVersionAsync(connection, transaction, cancellationToken);
        if (version > CurrentVersion)
            throw new InvalidDataException(
                $"Profile schema version {version} is newer than supported version {CurrentVersion}.");

        if (version == 0)
        {
            if (await UserTableCountAsync(connection, transaction, cancellationToken) == 0)
                await CreateVersionOneAsync(connection, transaction, cancellationToken);
            else
                await ValidateVersionOneAsync(connection, transaction, cancellationToken);
            await SetVersionAsync(connection, transaction, 1, cancellationToken);
            version = 1;
        }

        if (version == 1)
        {
            await CreateVersionTwoAsync(connection, transaction, cancellationToken);
            await SetVersionAsync(connection, transaction, 2, cancellationToken);
            version = 2;
        }

        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported profile schema version {version}.");
        await ValidateCurrentAsync(connection, transaction, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CreateVersionOneAsync(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, """
            CREATE TABLE "ProfileRevisions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ProfileRevisions" PRIMARY KEY AUTOINCREMENT,
                "ProfileId" TEXT NOT NULL,
                "Version" INTEGER NOT NULL,
                "Payload" BLOB NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_ProfileRevisions_ProfileId_Version"
                ON "ProfileRevisions" ("ProfileId", "Version");
            CREATE TABLE "PendingPatches" (
                "PatchId" TEXT NOT NULL CONSTRAINT "PK_PendingPatches" PRIMARY KEY,
                "ProfileId" TEXT NOT NULL,
                "BaseVersion" INTEGER NOT NULL,
                "Payload" BLOB NOT NULL,
                "Status" TEXT NOT NULL,
                "ProposedAt" TEXT NOT NULL
            );
            """, cancellationToken);
    }

    private static async Task CreateVersionTwoAsync(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS "ProfileAuditEntries" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ProfileAuditEntries" PRIMARY KEY AUTOINCREMENT,
                "ProfileId" TEXT NOT NULL,
                "ProfileVersion" INTEGER NOT NULL,
                "Operation" TEXT NOT NULL,
                "CorrelationId" TEXT NULL,
                "OccurredAt" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_ProfileAuditEntries_ProfileId_ProfileVersion"
                ON "ProfileAuditEntries" ("ProfileId", "ProfileVersion");
            """, cancellationToken);
    }

    private static Task ValidateVersionOneAsync(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken cancellationToken) =>
        ValidateTablesAsync(connection, transaction,
        [
            ("ProfileRevisions", new[] { "Id", "ProfileId", "Version", "Payload", "CreatedAt" }),
            ("PendingPatches", new[] { "PatchId", "ProfileId", "BaseVersion", "Payload", "Status", "ProposedAt" })
        ], cancellationToken);

    private static Task ValidateCurrentAsync(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken cancellationToken) =>
        ValidateTablesAsync(connection, transaction,
        [
            ("ProfileRevisions", new[] { "Id", "ProfileId", "Version", "Payload", "CreatedAt" }),
            ("PendingPatches", new[] { "PatchId", "ProfileId", "BaseVersion", "Payload", "Status", "ProposedAt" }),
            ("ProfileAuditEntries", new[] { "Id", "ProfileId", "ProfileVersion", "Operation", "CorrelationId", "OccurredAt" })
        ], cancellationToken);

    private static async Task ValidateTablesAsync(SqliteConnection connection,
        SqliteTransaction transaction, IEnumerable<(string Table, string[] Columns)> tables,
        CancellationToken cancellationToken)
    {
        foreach (var (table, expectedColumns) in tables)
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
                    $"Profile schema table {table} is missing required columns: {string.Join(", ", missing)}.");
        }
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

    private static Task SetVersionAsync(SqliteConnection connection, SqliteTransaction transaction,
        int version, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, $"PRAGMA user_version = {version};", cancellationToken);

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction,
        string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
