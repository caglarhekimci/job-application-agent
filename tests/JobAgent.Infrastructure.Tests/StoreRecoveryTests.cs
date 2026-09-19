using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobAgent.Core;
using JobAgent.Core.Applications;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Jobs;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Tests;

public sealed class StoreRecoveryTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-store-recovery-" + Guid.NewGuid().ToString("N"));
    private readonly TestProtector protector = new();
    private string ProfilesPath => Path.Combine(root, "profiles.db");
    private string JobsPath => Path.Combine(root, "jobs.db");
    private string JournalPath => Path.Combine(root, "applications.db");

    [Fact]
    public async Task ProfileUpgrade_CreatesBoundProtectedBackupAndExplicitRestoreRecoversLegacyData()
    {
        Directory.CreateDirectory(root);
        var profile = SyntheticData.Profile();
        var originalPayload = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(profile, Json));
        await CreateLegacyProfileAsync(profile, originalPayload);
        var repository = ProfileRepository();

        await repository.InitializeAsync();

        var backupPath = ProfilesPath + ".preupgrade-v0.protected";
        Assert.True(File.Exists(backupPath));
        var backupText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(backupPath));
        Assert.DoesNotContain("ProfileRevisions", backupText, StringComparison.Ordinal);
        Assert.DoesNotContain(profile.Email, backupText, StringComparison.Ordinal);
        await ExecuteAsync(ProfilesPath, "DELETE FROM ProfileRevisions;");
        Assert.Null(await repository.GetLatestAsync(profile.Id));

        await repository.RestoreLatestBackupAsync();
        await repository.InitializeAsync();

        Assert.Equal(profile.Email, (await repository.GetLatestAsync(profile.Id))!.Email);
        Assert.Equal(originalPayload, await ScalarBytesAsync(ProfilesPath,
            "SELECT Payload FROM ProfileRevisions WHERE ProfileId=$id;", profile.Id));
    }

    [Fact]
    public async Task ProfileDeletion_InvalidatesWholeStoreRecoverySnapshots()
    {
        Directory.CreateDirectory(root);
        var profile = SyntheticData.Profile();
        await CreateLegacyProfileAsync(profile,
            protector.Protect(JsonSerializer.SerializeToUtf8Bytes(profile, Json)));
        var repository = ProfileRepository();
        await repository.InitializeAsync();
        var backupPath = ProfilesPath + ".preupgrade-v0.protected";
        Assert.True(File.Exists(backupPath));

        await repository.DeleteAsync(profile.Id);

        Assert.False(File.Exists(backupPath));
        Assert.Null(await repository.GetLatestAsync(profile.Id));
    }

    [Fact]
    public async Task BackupForDifferentStoreKind_IsRejectedWithoutReplacingTarget()
    {
        Directory.CreateDirectory(root);
        var profile = SyntheticData.Profile();
        await CreateLegacyProfileAsync(profile,
            protector.Protect(JsonSerializer.SerializeToUtf8Bytes(profile, Json)));
        await ProfileRepository().InitializeAsync();
        var job = ReviewedJob("job-kind");
        var jobPayload = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(job, Json));
        await CreateLegacyJobAsync(job, jobPayload);
        File.Copy(ProfilesPath + ".preupgrade-v0.protected", JobsPath + ".preupgrade-v0.protected");
        var before = await File.ReadAllBytesAsync(JobsPath);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => JobRepository().RestoreLatestBackupAsync());

        Assert.Contains("store", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await File.ReadAllBytesAsync(JobsPath));
    }

    [Fact]
    public async Task LegacyJobStore_MigratesWithoutChangingPayloadAndWritesMetadataAudit()
    {
        Directory.CreateDirectory(root);
        var job = ReviewedJob("job-legacy");
        var originalPayload = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(job, Json));
        await CreateLegacyJobAsync(job, originalPayload);
        var repository = JobRepository();

        await repository.InitializeAsync();

        Assert.Equal(1, await ScalarLongAsync(JobsPath, "PRAGMA user_version;"));
        Assert.Equal(originalPayload, await ScalarBytesAsync(JobsPath, "SELECT Payload FROM Jobs LIMIT 1;"));
        Assert.Equal(1, await ScalarLongAsync(JobsPath, "SELECT Revision FROM Jobs LIMIT 1;"));

        await repository.SaveAsync(job with { Title = "Updated private title", LastCheckedAt = FixedNow.AddHours(1) });

        Assert.Equal(2, await ScalarLongAsync(JobsPath, "SELECT Revision FROM Jobs LIMIT 1;"));
        Assert.Equal("JobUpdated", await ScalarStringAsync(JobsPath, "SELECT Operation FROM JobAuditEntries ORDER BY Id LIMIT 1;"));
        var auditDdl = await ScalarStringAsync(JobsPath, "SELECT sql FROM sqlite_master WHERE type='table' AND name='JobAuditEntries';");
        var auditValues = await ScalarStringAsync(JobsPath, """
            SELECT group_concat(EntityRef || '|' || Operation || '|' || Actor || '|' ||
                coalesce(CorrelationId, '') || '|' || OccurredAt, ';') FROM JobAuditEntries;
            """);
        Assert.DoesNotContain("Payload", auditDdl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(job.Employer, auditDdl, StringComparison.Ordinal);
        Assert.DoesNotContain(job.Employer, auditValues, StringComparison.Ordinal);
        Assert.Equal(FixedNow, DateTimeOffset.Parse(await ScalarStringAsync(JobsPath,
            "SELECT OccurredAt FROM JobAuditEntries ORDER BY Id LIMIT 1;")));
    }

    [Fact]
    public async Task JobFutureVersion_FailsBeforeTablesOrBackupAreCreated()
    {
        Directory.CreateDirectory(root);
        await ExecuteAsync(JobsPath, "CREATE TABLE Sentinel(Value TEXT); INSERT INTO Sentinel VALUES('keep'); PRAGMA user_version=2;");

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => JobRepository().InitializeAsync());

        Assert.Contains("newer", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await ScalarLongAsync(JobsPath, "PRAGMA user_version;"));
        Assert.Equal(0, await ScalarLongAsync(JobsPath, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Jobs';"));
        Assert.False(File.Exists(JobsPath + ".preupgrade-v0.protected"));
    }

    [Fact]
    public async Task JobAuditFailure_RollsBackPayloadRevisionAndAudit()
    {
        Directory.CreateDirectory(root);
        var repository = JobRepository();
        await repository.InitializeAsync();
        var job = ReviewedJob("job-rollback");
        await repository.SaveAsync(job);
        await ExecuteAsync(JobsPath, """
            CREATE TRIGGER FailJobAudit BEFORE INSERT ON JobAuditEntries
            BEGIN SELECT RAISE(ABORT, 'forced audit failure'); END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.SaveAsync(job with { Title = "Must roll back" }));

        Assert.Equal(job.Title, (await repository.GetAsync(job.DuplicateKey))!.Title);
        Assert.Equal(1, await ScalarLongAsync(JobsPath, "SELECT Revision FROM Jobs LIMIT 1;"));
        Assert.Equal(1, await ScalarLongAsync(JobsPath, "SELECT COUNT(*) FROM JobAuditEntries;"));
    }

    [Fact]
    public async Task LegacyJournal_MigratesWithoutChangingBodyAndAuditsClaimTransactionally()
    {
        Directory.CreateDirectory(root);
        var record = ReadyApplication();
        var body = JsonSerializer.Serialize(record);
        await CreateLegacyJournalAsync(record, body);
        var journal = new ApplicationJournal(JournalPath, new FixedTimeProvider(FixedNow));

        await journal.InitializeAsync();

        Assert.Equal(1, await ScalarLongAsync(JournalPath, "PRAGMA user_version;"));
        Assert.Equal(body, await ScalarStringAsync(JournalPath, "SELECT Body FROM Applications LIMIT 1;"));
        Assert.True(await journal.ClaimSubmissionAsync(record.Draft.Id, FixedNow));
        Assert.Equal("StateTransition", await ScalarStringAsync(JournalPath,
            "SELECT Operation FROM ApplicationAuditEntries ORDER BY Id LIMIT 1;"));
        Assert.Equal((long)ApplicationStatus.Submitting, await ScalarLongAsync(JournalPath,
            "SELECT ToState FROM ApplicationAuditEntries ORDER BY Id LIMIT 1;"));
        var auditDdl = await ScalarStringAsync(JournalPath,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='ApplicationAuditEntries';");
        var auditValues = await ScalarStringAsync(JournalPath, """
            SELECT group_concat(ApplicationId || '|' || Operation || '|' || Actor || '|' ||
                coalesce(CorrelationId, '') || '|' || OccurredAt, ';') FROM ApplicationAuditEntries;
            """);
        Assert.DoesNotContain("Body", auditDdl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidate@example.invalid", auditDdl, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate@example.invalid", auditValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JournalFutureVersion_FailsBeforeSchemaMutation()
    {
        Directory.CreateDirectory(root);
        await ExecuteAsync(JournalPath, "CREATE TABLE Sentinel(Value TEXT); INSERT INTO Sentinel VALUES('keep'); PRAGMA user_version=2;");
        var journal = new ApplicationJournal(JournalPath);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => journal.InitializeAsync());

        Assert.Contains("newer", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await ScalarLongAsync(JournalPath, "PRAGMA user_version;"));
        Assert.Equal(0, await ScalarLongAsync(JournalPath,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Applications';"));
    }

    [Fact]
    public async Task JournalAuditFailure_RollsBackStateAndRevision()
    {
        Directory.CreateDirectory(root);
        var journal = new ApplicationJournal(JournalPath, new FixedTimeProvider(FixedNow));
        await journal.InitializeAsync();
        var record = ReadyApplication();
        await journal.CreateAsync(record);
        await ExecuteAsync(JournalPath, """
            CREATE TRIGGER FailApplicationAudit BEFORE INSERT ON ApplicationAuditEntries
            BEGIN SELECT RAISE(ABORT, 'forced audit failure'); END;
            """);

        await Assert.ThrowsAnyAsync<Exception>(() => journal.ClaimSubmissionAsync(record.Draft.Id, FixedNow));

        Assert.Equal(ApplicationStatus.AwaitingSubmissionApproval, (await journal.GetAsync(record.Draft.Id))!.Draft.Status);
        Assert.Equal(1, await ScalarLongAsync(JournalPath, "SELECT Revision FROM Applications LIMIT 1;"));
        Assert.Equal(1, await ScalarLongAsync(JournalPath, "SELECT COUNT(*) FROM ApplicationAuditEntries;"));
    }

    private ProfileRepository ProfileRepository() => new(new()
    {
        DatabasePath = ProfilesPath,
        CheckoutRoot = Environment.CurrentDirectory
    }, protector, new FixedTimeProvider(FixedNow));

    private JobRepository JobRepository() => new(new()
    {
        DatabasePath = JobsPath,
        CheckoutRoot = Environment.CurrentDirectory
    }, protector, new FixedTimeProvider(FixedNow));

    private static JobPosting ReviewedJob(string id)
    {
        var proposal = JobPostingImporter.ProposeProvidedText(id, "Private Synthetic Employer", "C# Engineer",
            "Mandatory: 3 years of professional C# experience.",
            $"https://careers.example.invalid/jobs/apply?jobId={id}", FixedNow, synthetic: true);
        return JobPostingImporter.ConfirmRequirements(proposal,
            proposal.SuggestedRequirements.Select(item => item.Id), FixedNow);
    }

    private static WorkflowRecord ReadyApplication()
    {
        var draft = new ApplicationDraft
        {
            JobKey = "synthetic:recovery",
            JobTitle = "Private title stays in body",
            Employer = "Private employer stays in body",
            Answers = new SortedDictionary<string, string> { ["contact.email"] = "candidate@example.invalid" },
            Synthetic = true,
            ResumeHash = "ABC",
            RecipientOrigin = "http://127.0.0.1:5179",
            ProfileVersion = 1,
            Status = ApplicationStatus.AwaitingSubmissionApproval
        };
        return new(draft, Submission: ApprovalPolicy.GrantFromUserInterface(draft,
            ApprovalPurpose.Submit, "session", FixedNow));
    }

    private async Task CreateLegacyProfileAsync(CandidateProfile profile, byte[] payload)
    {
        await ExecuteAsync(ProfilesPath, """
            CREATE TABLE ProfileRevisions (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProfileId TEXT NOT NULL, Version INTEGER NOT NULL, Payload BLOB NOT NULL, CreatedAt TEXT NOT NULL);
            CREATE UNIQUE INDEX IX_ProfileRevisions_ProfileId_Version ON ProfileRevisions(ProfileId, Version);
            CREATE TABLE PendingPatches (PatchId TEXT PRIMARY KEY, ProfileId TEXT NOT NULL, BaseVersion INTEGER NOT NULL, Payload BLOB NOT NULL, Status TEXT NOT NULL, ProposedAt TEXT NOT NULL);
            """);
        await using var connection = await OpenAsync(ProfilesPath);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ProfileRevisions(ProfileId,Version,Payload,CreatedAt) VALUES($id,$version,$payload,$at);";
        command.Parameters.AddWithValue("$id", profile.Id);
        command.Parameters.AddWithValue("$version", profile.Version);
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$at", FixedNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateLegacyJobAsync(JobPosting job, byte[] payload)
    {
        await ExecuteAsync(JobsPath, """
            CREATE TABLE Jobs (Id INTEGER PRIMARY KEY AUTOINCREMENT, IdentityHash TEXT NOT NULL, Payload BLOB NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
            CREATE UNIQUE INDEX IX_Jobs_IdentityHash ON Jobs(IdentityHash);
            """);
        await using var connection = await OpenAsync(JobsPath);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Jobs(IdentityHash,Payload,CreatedAt,UpdatedAt) VALUES($hash,$payload,$at,$at);";
        command.Parameters.AddWithValue("$hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(job.DuplicateKey))));
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$at", FixedNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateLegacyJournalAsync(WorkflowRecord record, string body)
    {
        await ExecuteAsync(JournalPath, """
            CREATE TABLE Applications (Id TEXT PRIMARY KEY, JobIdentity TEXT NOT NULL, State INTEGER NOT NULL, Body TEXT NOT NULL, Revision INTEGER NOT NULL);
            CREATE UNIQUE INDEX IX_Applications_JobIdentity ON Applications(JobIdentity);
            """);
        await using var connection = await OpenAsync(JournalPath);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Applications VALUES($id,$job,$state,$body,1);";
        command.Parameters.AddWithValue("$id", record.Draft.Id);
        command.Parameters.AddWithValue("$job", record.Draft.ProfileId + ":" + record.Draft.JobKey);
        command.Parameters.AddWithValue("$state", (int)record.Draft.Status);
        command.Parameters.AddWithValue("$body", body);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(string path, string sql)
    {
        await using var connection = await OpenAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLongAsync(string path, string sql)
    {
        await using var connection = await OpenAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(string path, string sql)
    {
        await using var connection = await OpenAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task<byte[]> ScalarBytesAsync(string path, string sql, Guid? id = null)
    {
        await using var connection = await OpenAsync(path);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (id is not null) command.Parameters.AddWithValue("$id", id.Value);
        return (byte[])(await command.ExecuteScalarAsync() ?? Array.Empty<byte>());
    }

    private static async Task<SqliteConnection> OpenAsync(string path)
    {
        var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        return connection;
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
        private static readonly byte[] Key = SHA256.HashData("private-store-test-key"u8.ToArray());
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
