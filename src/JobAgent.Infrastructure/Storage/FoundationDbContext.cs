using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Storage;

public sealed class ProfileRevisionEntity
{
    public long Id { get; set; }
    public Guid ProfileId { get; set; }
    public int Version { get; set; }
    public required byte[] Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PendingPatchEntity
{
    public Guid PatchId { get; set; }
    public Guid ProfileId { get; set; }
    public int BaseVersion { get; set; }
    public required byte[] Payload { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset ProposedAt { get; set; }
}

public sealed class ProfileAuditEntity
{
    public long Id { get; set; }
    public Guid ProfileId { get; set; }
    public int ProfileVersion { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Guid? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class FoundationDbContext(DbContextOptions<FoundationDbContext> options) : DbContext(options)
{
    public DbSet<ProfileRevisionEntity> ProfileRevisions => Set<ProfileRevisionEntity>();
    public DbSet<PendingPatchEntity> PendingPatches => Set<PendingPatchEntity>();
    public DbSet<ProfileAuditEntity> ProfileAuditEntries => Set<ProfileAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileRevisionEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ProfileId, item.Version }).IsUnique();
            entity.Property(item => item.Payload).IsRequired();
        });
        modelBuilder.Entity<PendingPatchEntity>(entity =>
        {
            entity.HasKey(item => item.PatchId);
            entity.Property(item => item.Payload).IsRequired();
        });
        modelBuilder.Entity<ProfileAuditEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ProfileId, item.ProfileVersion });
            entity.Property(item => item.Operation).IsRequired();
        });
    }
}

public sealed class FoundationDbContextFactory
{
    private readonly DbContextOptions<FoundationDbContext> _options;

    public FoundationDbContextFactory(string databasePath)
    {
        DatabasePath = databasePath;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        _options = new DbContextOptionsBuilder<FoundationDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public string DatabasePath { get; }
    public FoundationDbContext CreateDbContext() => new(_options);
}
