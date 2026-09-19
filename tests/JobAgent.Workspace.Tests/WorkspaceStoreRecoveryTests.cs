using System.Security.Cryptography;
using System.Text;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Microsoft.Data.Sqlite;
using Xunit;

namespace JobAgent.Workspace.Tests;

public sealed class WorkspaceStoreRecoveryTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 20, 8, 15, 0, TimeSpan.Zero);
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-workspace-recovery-" + Guid.NewGuid().ToString("N"));
    private readonly TestProtector protector = new();
    private string DatabasePath => Path.Combine(root, "workspace.db");
    private string BackupPath => DatabasePath + ".preupgrade-v0.protected";

    [Fact]
    public async Task LegacyWorkspace_MigratesWithoutChangingPayloadAndCanBeExplicitlyRestored()
    {
        Directory.CreateDirectory(root);
        var originalPayload = protector.Protect("{}"u8);
        await CreateLegacyAsync(originalPayload);
        using var workspace = Open();

        var view = await workspace.GetAsync();

        Assert.Equal(3, view.Revision);
        Assert.Equal(1, await ScalarLongAsync("PRAGMA user_version;"));
        Assert.Equal(originalPayload, await ScalarBytesAsync("SELECT Payload FROM Workspace WHERE Id=1;"));
        Assert.True(File.Exists(BackupPath));
        Assert.DoesNotContain("Workspace", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(BackupPath)), StringComparison.Ordinal);

        await workspace.ImportAsync(new MemoryStream("Synthetic User\nProfessional Experience: C# developer 2020-2024"u8.ToArray()), "candidate.txt");
        Assert.Equal(4, (await workspace.GetAsync()).Revision);

        await workspace.RestoreLatestBackupAsync();

        Assert.Equal(3, (await workspace.GetAsync()).Revision);
        Assert.Equal(originalPayload, await ScalarBytesAsync("SELECT Payload FROM Workspace WHERE Id=1;"));
    }

    [Fact]
    public async Task WorkspaceFutureVersion_FailsBeforeSchemaBackupOrPragmaMutation()
    {
        Directory.CreateDirectory(root);
        await using (var connection = await OpenAsync())
        {
            await ExecuteAsync(connection, "CREATE TABLE Sentinel (Value TEXT NOT NULL); INSERT INTO Sentinel VALUES ('keep'); PRAGMA user_version=2;");
        }
        using var workspace = Open();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => workspace.GetAsync());

        Assert.Contains("newer", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await ScalarLongAsync("PRAGMA user_version;"));
        Assert.Equal(1, await ScalarLongAsync("SELECT COUNT(*) FROM Sentinel;"));
        Assert.Equal(0, await ScalarLongAsync("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Workspace';"));
        Assert.False(File.Exists(BackupPath));
    }

    [Fact]
    public async Task WorkspaceMigrationFailure_RollsBackAndPreservesFirstProtectedBackup()
    {
        Directory.CreateDirectory(root);
        var originalPayload = protector.Protect("{}"u8);
        await CreateLegacyAsync(originalPayload);
        await using (var connection = await OpenAsync())
        {
            await ExecuteAsync(connection, "CREATE TABLE WorkspaceAuditEntries (Id INTEGER PRIMARY KEY, WrongColumn TEXT NOT NULL);");
        }
        using var workspace = Open();

        await Assert.ThrowsAnyAsync<Exception>(() => workspace.GetAsync());
        var firstBackup = await File.ReadAllBytesAsync(BackupPath);
        await Assert.ThrowsAnyAsync<Exception>(() => workspace.GetAsync());

        Assert.Equal(firstBackup, await File.ReadAllBytesAsync(BackupPath));
        Assert.Equal(0, await ScalarLongAsync("PRAGMA user_version;"));
        Assert.Equal(originalPayload, await ScalarBytesAsync("SELECT Payload FROM Workspace WHERE Id=1;"));
        Assert.Equal(0, await ScalarLongAsync("SELECT COUNT(*) FROM pragma_table_info('Workspace') WHERE name='SchemaMarker';"));
    }

    [Fact]
    public async Task WorkspaceAuditFailure_RollsBackPayloadRevisionAndAudit()
    {
        using var workspace = Open();
        await workspace.GetAsync();
        await using (var connection = await OpenAsync())
        {
            await ExecuteAsync(connection, """
                CREATE TRIGGER FailWorkspaceAudit BEFORE INSERT ON WorkspaceAuditEntries
                BEGIN SELECT RAISE(ABORT, 'forced audit failure'); END;
                """);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => workspace.ImportAsync(
            new MemoryStream("Synthetic User\nProfessional Experience: C# developer 2020-2024"u8.ToArray()), "candidate.txt"));

        Assert.Equal(0, (await workspace.GetAsync()).Revision);
        Assert.Equal(0, await ScalarLongAsync("SELECT COUNT(*) FROM Workspace;"));
        Assert.Equal(0, await ScalarLongAsync("SELECT COUNT(*) FROM WorkspaceAuditEntries;"));
    }

    [Fact]
    public async Task WorkspaceAuditContainsOnlyMetadata()
    {
        using var workspace = Open();
        await workspace.ImportAsync(new MemoryStream("Private Synthetic Name\nProfessional Experience: C# developer 2020-2024"u8.ToArray()), "candidate.txt");

        var auditSql = await ScalarStringAsync("SELECT sql FROM sqlite_master WHERE type='table' AND name='WorkspaceAuditEntries';");
        var operation = await ScalarStringAsync("SELECT Operation FROM WorkspaceAuditEntries ORDER BY Id LIMIT 1;");
        var auditValues = await ScalarStringAsync("""
            SELECT group_concat(EntityRef || '|' || Operation || '|' || Actor || '|' ||
                coalesce(CorrelationId, '') || '|' || OccurredAt, ';') FROM WorkspaceAuditEntries;
            """);

        Assert.DoesNotContain("Payload", auditSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Private Synthetic Name", auditSql, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Synthetic Name", auditValues, StringComparison.Ordinal);
        Assert.Equal("WorkspaceUpdated", operation);
        Assert.Equal(FixedNow.ToString("O"), await ScalarStringAsync("SELECT OccurredAt FROM WorkspaceAuditEntries ORDER BY Id LIMIT 1;"));
    }

    [Fact]
    public async Task WorkspaceDeletion_InvalidatesItsRecoverySnapshot()
    {
        Directory.CreateDirectory(root);
        await CreateLegacyAsync(protector.Protect("{}"u8));
        using var workspace = Open();
        var migrated = await workspace.GetAsync();
        Assert.True(File.Exists(BackupPath));

        await workspace.DeleteAsync(migrated.Revision);

        Assert.False(File.Exists(BackupPath));
        Assert.Null((await workspace.GetAsync()).Document);
    }

    private LocalWorkspace Open() => new(root, Environment.CurrentDirectory, protector,
        timeProvider: new FixedTimeProvider(FixedNow));

    private async Task CreateLegacyAsync(byte[] payload)
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE Workspace (Id INTEGER PRIMARY KEY CHECK(Id=1), Revision INTEGER NOT NULL, Payload BLOB NOT NULL);");
        await using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Workspace VALUES(1, 3, $payload);";
        insert.Parameters.AddWithValue("$payload", payload);
        await insert.ExecuteNonQueryAsync();
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await connection.OpenAsync();
        return connection;
    }

    private async Task<long> ScalarLongAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task<string> ScalarStringAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private async Task<byte[]> ScalarBytesAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (byte[])(await command.ExecuteScalarAsync() ?? Array.Empty<byte>());
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestProtector : IPayloadProtector
    {
        private static readonly byte[] Key = SHA256.HashData("workspace-store-test-key"u8.ToArray());
        public bool IsPlaintext => false;
        public byte[] Protect(ReadOnlySpan<byte> plaintext) => Transform(plaintext);
        public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload) => Transform(protectedPayload);
        private static byte[] Transform(ReadOnlySpan<byte> value)
        {
            var result = value.ToArray();
            for (var index = 0; index < result.Length; index++) result[index] ^= Key[index % Key.Length];
            return result;
        }
    }
}
