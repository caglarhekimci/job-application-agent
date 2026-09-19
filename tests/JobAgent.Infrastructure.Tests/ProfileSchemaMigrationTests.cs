using System.Text.Json;
using JobAgent.Core;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobAgent.Infrastructure.Tests;

public sealed class ProfileSchemaMigrationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 19, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task LegacyEnsureCreatedDatabase_UpgradesWithoutDataLoss()
    {
        using var temp = new TemporaryStore();
        var protector = CreateProtector();
        var profile = SyntheticData.Profile();
        await CreateLegacyStoreAsync(temp.DatabasePath, protector, profile);
        var repository = Repository(temp, protector);

        await repository.InitializeAsync();

        var loaded = await repository.GetLatestAsync(profile.Id);
        Assert.NotNull(loaded);
        Assert.Equal(profile.Email, loaded.Email);
        Assert.Equal(profile.Version, loaded.Version);
        await using var connection = await OpenAsync(temp.DatabasePath);
        Assert.Equal(ProfileSchema.CurrentVersion, await ScalarLongAsync(connection, "PRAGMA user_version;"));
        Assert.Equal(1, await ScalarLongAsync(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ProfileAuditEntries';"));
    }

    [Fact]
    public async Task FutureSchemaVersion_FailsClosedBeforeMutation()
    {
        using var temp = new TemporaryStore();
        await using (var connection = await OpenAsync(temp.DatabasePath))
        {
            await ExecuteAsync(connection, "CREATE TABLE Sentinel (Value TEXT NOT NULL);");
            await ExecuteAsync(connection, "INSERT INTO Sentinel (Value) VALUES ('keep');");
            await ExecuteAsync(connection, $"PRAGMA user_version = {ProfileSchema.CurrentVersion + 1};");
        }
        var repository = Repository(temp, CreateProtector());

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => repository.InitializeAsync());

        Assert.Contains("newer", error.Message, StringComparison.OrdinalIgnoreCase);
        await using var check = await OpenAsync(temp.DatabasePath);
        Assert.Equal(ProfileSchema.CurrentVersion + 1, await ScalarLongAsync(check, "PRAGMA user_version;"));
        Assert.Equal(1, await ScalarLongAsync(check, "SELECT COUNT(*) FROM Sentinel;"));
        Assert.Equal(0, await ScalarLongAsync(check,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ProfileRevisions';"));
    }

    [Fact]
    public async Task CurrentSchemaMissingRequiredTable_IsRejected()
    {
        using var temp = new TemporaryStore();
        await using (var connection = await OpenAsync(temp.DatabasePath))
        {
            await ExecuteAsync(connection, "CREATE TABLE Sentinel (Value TEXT NOT NULL);");
            await ExecuteAsync(connection, $"PRAGMA user_version = {ProfileSchema.CurrentVersion};");
        }

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            Repository(temp, CreateProtector()).InitializeAsync());

        Assert.Contains("ProfileRevisions", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppliedPatch_WritesDeterministicMetadataOnlyAudit()
    {
        using var temp = new TemporaryStore();
        var repository = Repository(temp, CreateProtector());
        await repository.InitializeAsync();
        var profile = SyntheticData.Profile();
        await repository.SaveInitialAsync(profile);
        var patch = new ProfilePatch
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ProfileId = profile.Id,
            BaseVersion = profile.Version,
            ProposedAt = FixedNow.AddMinutes(-1),
            ProposedProfile = profile with { FullName = "Updated Synthetic Candidate" }
        };

        var applied = await repository.ApplyPatchAsync(patch, locallyApproved: true);

        Assert.True(applied.Applied);
        await using var context = new FoundationDbContextFactory(temp.DatabasePath).CreateDbContext();
        var audit = await context.ProfileAuditEntries.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        Assert.Collection(audit,
            created =>
            {
                Assert.Equal("ProfileCreated", created.Operation);
                Assert.Equal(1, created.ProfileVersion);
                Assert.Null(created.CorrelationId);
            },
            proposed =>
            {
                Assert.Equal("PatchProposed", proposed.Operation);
                Assert.Equal(1, proposed.ProfileVersion);
                Assert.Equal(patch.Id, proposed.CorrelationId);
            },
            updated =>
            {
                Assert.Equal("PatchApplied", updated.Operation);
                Assert.Equal(2, updated.ProfileVersion);
                Assert.Equal(patch.Id, updated.CorrelationId);
            });
        Assert.All(audit, entry =>
        {
            Assert.Equal(profile.Id, entry.ProfileId);
            Assert.Equal(FixedNow, entry.OccurredAt);
            Assert.DoesNotContain("Synthetic Candidate", entry.Operation, StringComparison.Ordinal);
            Assert.DoesNotContain(profile.Email, entry.Operation, StringComparison.Ordinal);
        });
        await using var connection = await OpenAsync(temp.DatabasePath);
        var auditDdl = await ScalarStringAsync(connection,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='ProfileAuditEntries';");
        Assert.DoesNotContain("Payload", auditDdl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteFailure_RollsBackAllProfileTables()
    {
        using var temp = new TemporaryStore();
        var repository = Repository(temp, CreateProtector());
        await repository.InitializeAsync();
        var profile = SyntheticData.Profile();
        await repository.SaveInitialAsync(profile);
        var patch = new ProfilePatch
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            ProfileId = profile.Id,
            BaseVersion = profile.Version,
            ProposedAt = FixedNow,
            ProposedProfile = profile with { FullName = "Pending Synthetic Candidate" }
        };
        await repository.SavePendingPatchAsync(patch);
        await using (var connection = await OpenAsync(temp.DatabasePath))
        {
            await ExecuteAsync(connection, """
                CREATE TRIGGER FailPendingPatchDelete BEFORE DELETE ON PendingPatches
                BEGIN SELECT RAISE(ABORT, 'forced delete failure'); END;
                """);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => repository.DeleteAsync(profile.Id));

        Assert.NotNull(await repository.GetLatestAsync(profile.Id));
        await using (var connection = await OpenAsync(temp.DatabasePath))
        {
            Assert.Equal(1, await ScalarLongAsync(connection,
                "SELECT COUNT(*) FROM ProfileRevisions WHERE ProfileId = $id;", profile.Id));
            Assert.Equal(1, await ScalarLongAsync(connection,
                "SELECT COUNT(*) FROM PendingPatches WHERE ProfileId = $id;", profile.Id));
            Assert.Equal(2, await ScalarLongAsync(connection,
                "SELECT COUNT(*) FROM ProfileAuditEntries WHERE ProfileId = $id;", profile.Id));
            await ExecuteAsync(connection, "DROP TRIGGER FailPendingPatchDelete;");
        }

        await repository.DeleteAsync(profile.Id);

        await using var final = await OpenAsync(temp.DatabasePath);
        Assert.Equal(0, await ScalarLongAsync(final,
            "SELECT COUNT(*) FROM ProfileRevisions WHERE ProfileId = $id;", profile.Id));
        Assert.Equal(0, await ScalarLongAsync(final,
            "SELECT COUNT(*) FROM PendingPatches WHERE ProfileId = $id;", profile.Id));
        Assert.Equal(0, await ScalarLongAsync(final,
            "SELECT COUNT(*) FROM ProfileAuditEntries WHERE ProfileId = $id;", profile.Id));
    }

    private static ProfileRepository Repository(TemporaryStore temp, IPayloadProtector protector) =>
        new(temp.Options(!OperatingSystem.IsWindows()), protector, new FixedTimeProvider(FixedNow));

    private static IPayloadProtector CreateProtector() => OperatingSystem.IsWindows()
        ? new WindowsDpapiPayloadProtector()
        : new SyntheticPlaintextPayloadProtector();

    private static async Task CreateLegacyStoreAsync(string path, IPayloadProtector protector,
        CandidateProfile profile)
    {
        var options = new DbContextOptionsBuilder<LegacyProfileContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        await using var context = new LegacyProfileContext(options);
        await context.Database.EnsureCreatedAsync();
        var json = JsonSerializer.SerializeToUtf8Bytes(profile, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        context.ProfileRevisions.Add(new()
        {
            ProfileId = profile.Id,
            Version = profile.Version,
            Payload = protector.Protect(json),
            CreatedAt = FixedNow.AddDays(-1)
        });
        await context.SaveChangesAsync();
    }

    private static async Task<SqliteConnection> OpenAsync(string path)
    {
        var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql, Guid? id = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (id is not null) command.Parameters.AddWithValue("$id", id.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> ScalarStringAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class LegacyProfileRevision
    {
        public long Id { get; set; }
        public Guid ProfileId { get; set; }
        public int Version { get; set; }
        public required byte[] Payload { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class LegacyPendingPatch
    {
        public Guid PatchId { get; set; }
        public Guid ProfileId { get; set; }
        public int BaseVersion { get; set; }
        public required byte[] Payload { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTimeOffset ProposedAt { get; set; }
    }

    private sealed class LegacyProfileContext(DbContextOptions<LegacyProfileContext> options) : DbContext(options)
    {
        public DbSet<LegacyProfileRevision> ProfileRevisions => Set<LegacyProfileRevision>();
        public DbSet<LegacyPendingPatch> PendingPatches => Set<LegacyPendingPatch>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LegacyProfileRevision>(entity =>
            {
                entity.ToTable("ProfileRevisions");
                entity.HasKey(item => item.Id);
                entity.HasIndex(item => new { item.ProfileId, item.Version }).IsUnique();
                entity.Property(item => item.Payload).IsRequired();
            });
            modelBuilder.Entity<LegacyPendingPatch>(entity =>
            {
                entity.ToTable("PendingPatches");
                entity.HasKey(item => item.PatchId);
                entity.Property(item => item.Payload).IsRequired();
            });
        }
    }
}
