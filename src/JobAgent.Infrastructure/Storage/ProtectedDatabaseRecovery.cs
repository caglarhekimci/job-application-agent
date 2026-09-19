using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Storage;

internal static class ProtectedDatabaseRecovery
{
    private const int EnvelopeVersion = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task CreateIfMissingAsync(SqliteConnection source, string databasePath,
        string storeKind, int schemaVersion, IPayloadProtector protector,
        CancellationToken cancellationToken)
    {
        var backupPath = BackupPath(databasePath, schemaVersion);
        if (File.Exists(backupPath))
        {
            await ReadEnvelopeAsync(backupPath, storeKind, schemaVersion, protector, cancellationToken);
            return;
        }

        var snapshotPath = databasePath + ".snapshot-" + Guid.NewGuid().ToString("N") + ".tmp";
        var protectedTempPath = backupPath + ".write-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var destinationConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = snapshotPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString();
            await using (var destination = new SqliteConnection(destinationConnectionString))
            {
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            var databaseBytes = await File.ReadAllBytesAsync(snapshotPath, cancellationToken);
            var envelope = new BackupEnvelope(EnvelopeVersion, storeKind, schemaVersion,
                Convert.ToHexString(SHA256.HashData(databaseBytes)), databaseBytes);
            var protectedBytes = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(envelope, Json));
            await WriteThroughAsync(protectedTempPath, protectedBytes, cancellationToken);
            try
            {
                File.Move(protectedTempPath, backupPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                await ReadEnvelopeAsync(backupPath, storeKind, schemaVersion, protector, cancellationToken);
            }
        }
        finally
        {
            DeleteIfPresent(snapshotPath);
            DeleteIfPresent(protectedTempPath);
        }
    }

    public static async Task RestoreLatestAsync(string databasePath, string storeKind,
        IReadOnlyList<int> supportedSourceVersions, IPayloadProtector protector,
        CancellationToken cancellationToken)
    {
        var candidates = supportedSourceVersions
            .Select(version => (Version: version, Path: BackupPath(databasePath, version)))
            .Where(candidate => File.Exists(candidate.Path))
            .OrderByDescending(candidate => File.GetLastWriteTimeUtc(candidate.Path))
            .ToArray();
        if (candidates.Length == 0)
            throw new FileNotFoundException("No protected pre-upgrade backup is available.");

        var selected = candidates[0];
        var envelope = await ReadEnvelopeAsync(selected.Path, storeKind, selected.Version,
            protector, cancellationToken);
        if (File.Exists(databasePath + "-wal") || File.Exists(databasePath + "-shm") ||
            File.Exists(databasePath + "-journal"))
            throw new InvalidOperationException(
                "Close the store and resolve SQLite journal or WAL files before restoring.");

        if (File.Exists(databasePath))
        {
            try
            {
                await using var exclusive = new FileStream(databasePath, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.None, 1, FileOptions.Asynchronous);
            }
            catch (IOException error)
            {
                throw new InvalidOperationException("Close every process using the store before restoring.", error);
            }
        }

        var restoreTempPath = databasePath + ".restore-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await WriteThroughAsync(restoreTempPath, envelope.DatabaseBytes, cancellationToken);
            await ValidateSqliteAsync(restoreTempPath, cancellationToken);
            File.Move(restoreTempPath, databasePath, overwrite: true);
        }
        finally
        {
            DeleteIfPresent(restoreTempPath);
        }
    }

    public static void InvalidateBackups(string databasePath,
        IReadOnlyList<int> supportedSourceVersions)
    {
        foreach (var version in supportedSourceVersions)
        {
            var path = BackupPath(databasePath, version);
            if (!File.Exists(path)) continue;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write,
                       FileShare.None, 1, FileOptions.WriteThrough))
            {
                stream.SetLength(0);
                stream.Flush(flushToDisk: true);
            }
            File.Delete(path);
        }
    }

    private static string BackupPath(string databasePath, int schemaVersion) =>
        databasePath + $".preupgrade-v{schemaVersion}.protected";

    private static async Task<BackupEnvelope> ReadEnvelopeAsync(string backupPath,
        string expectedStoreKind, int expectedSchemaVersion, IPayloadProtector protector,
        CancellationToken cancellationToken)
    {
        BackupEnvelope? envelope;
        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(backupPath, cancellationToken);
            envelope = JsonSerializer.Deserialize<BackupEnvelope>(protector.Unprotect(protectedBytes), Json);
        }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException)
        {
            throw new InvalidDataException("The protected store backup is invalid.", error);
        }
        if (envelope is null || envelope.EnvelopeVersion != EnvelopeVersion ||
            !string.Equals(envelope.StoreKind, expectedStoreKind, StringComparison.Ordinal) ||
            envelope.SchemaVersion != expectedSchemaVersion)
            throw new InvalidDataException("The protected backup does not match the expected store and schema.");
        try
        {
            var actualHash = SHA256.HashData(envelope.DatabaseBytes);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(envelope.ContentHash), actualHash))
                throw new InvalidDataException("The protected backup content hash is invalid.");
        }
        catch (FormatException error)
        {
            throw new InvalidDataException("The protected backup content hash is invalid.", error);
        }
        return envelope;
    }

    private static async Task ValidateSqliteAsync(string path, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The protected backup is not a valid SQLite snapshot.");
    }

    private static async Task WriteThroughAsync(string path, byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static void DeleteIfPresent(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }

    private sealed record BackupEnvelope(int EnvelopeVersion, string StoreKind,
        int SchemaVersion, string ContentHash, byte[] DatabaseBytes);
}
