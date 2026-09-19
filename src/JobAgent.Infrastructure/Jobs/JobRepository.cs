using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobAgent.Core.Jobs;
using JobAgent.Core.Permissions;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobAgent.Infrastructure.Jobs;

public sealed record JobStoreOptions
{
    public required string DatabasePath { get; init; }
    public required string CheckoutRoot { get; init; }
    public bool AllowSyntheticPlaintextForLinuxTests { get; init; }
}

public sealed class JobRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly JobDbContextFactory _factory;
    private readonly IPayloadProtector _protector;
    private readonly TimeProvider _timeProvider;

    public JobRepository(JobStoreOptions options, IPayloadProtector protector,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(protector);
        var databasePath = Path.GetFullPath(options.DatabasePath);
        var checkoutRoot = Path.GetFullPath(options.CheckoutRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (databasePath.StartsWith(checkoutRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The jobs database must be stored outside the source checkout.");
        if (protector.IsPlaintext && (OperatingSystem.IsWindows() ||
            !options.AllowSyntheticPlaintextForLinuxTests))
            throw new InvalidOperationException(
                "Plaintext protection is limited to explicitly enabled synthetic Linux tests.");

        Options = options;
        _factory = new(databasePath);
        _protector = protector;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public JobStoreOptions Options { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(Options.DatabasePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        await JobSchema.InitializeAsync(_factory.DatabasePath, _protector, cancellationToken);
    }

    public Task RestoreLatestBackupAsync(CancellationToken cancellationToken = default) =>
        ProtectedDatabaseRecovery.RestoreLatestAsync(_factory.DatabasePath, "jobs", [0],
            _protector, cancellationToken);

    public async Task SaveAsync(JobPosting job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        Validate(job);
        var identityHash = Hash(job.DuplicateKey);
        var payload = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(job, JsonOptions));
        var now = _timeProvider.GetUtcNow();
        await using var context = _factory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var existing = await context.Jobs.SingleOrDefaultAsync(item => item.IdentityHash == identityHash,
            cancellationToken);
        var previousRevision = existing?.Revision ?? 0;
        var newRevision = previousRevision + 1;
        if (existing is null)
        {
            context.Jobs.Add(new()
            {
                IdentityHash = identityHash,
                Payload = payload,
                CreatedAt = now,
                UpdatedAt = now,
                Revision = newRevision
            });
        }
        else
        {
            existing.Payload = payload;
            existing.UpdatedAt = now;
            existing.Revision = newRevision;
        }
        context.JobAuditEntries.Add(new()
        {
            EntityRef = identityHash,
            PreviousRevision = previousRevision,
            NewRevision = newRevision,
            Operation = existing is null ? "JobCreated" : "JobUpdated",
            Actor = "LocalRepository",
            OccurredAt = now
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<JobPosting?> GetAsync(string duplicateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(duplicateKey);
        var identityHash = Hash(duplicateKey);
        await using var context = _factory.CreateDbContext();
        var row = await context.Jobs.AsNoTracking().SingleOrDefaultAsync(
            item => item.IdentityHash == identityHash, cancellationToken);
        return row is null ? null : Deserialize(row.Payload);
    }

    public async Task<IReadOnlyList<JobPosting>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateDbContext();
        var rows = await context.Jobs.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        return rows.Select(item => Deserialize(item.Payload)).ToArray();
    }

    private void Validate(JobPosting job)
    {
        if (_protector.IsPlaintext && !job.Synthetic)
            throw new InvalidOperationException("Plaintext test protection accepts synthetic jobs only.");
        if (string.IsNullOrWhiteSpace(job.Id) || string.IsNullOrWhiteSpace(job.Employer) ||
            string.IsNullOrWhiteSpace(job.Title) || string.IsNullOrWhiteSpace(job.Text))
            throw new InvalidDataException("Job identity, employer, title and text are required.");
        if (!string.Equals(JobUrlCanonicalizer.Normalize(job.SourceUrl), job.CanonicalUrl,
            StringComparison.Ordinal))
            throw new InvalidDataException("Stored canonical URL does not match the source URL.");
        var textHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(job.Text)));
        if (!string.Equals(textHash, job.TextHash, StringComparison.Ordinal))
            throw new InvalidDataException("Stored job text hash does not match the text.");
        if (job.Requirements.Any(item => item.ReviewStatus != RequirementReviewStatus.Confirmed))
            throw new InvalidDataException("Only user-confirmed requirements may be persisted.");
        foreach (var requirement in job.Requirements) JobRequirementPolicy.Validate(requirement);
        SourcePermissionPolicy.ValidateForPersistence(job.SourcePermission, _timeProvider.GetUtcNow());
    }

    private JobPosting Deserialize(byte[] payload) =>
        JsonSerializer.Deserialize<JobPosting>(_protector.Unprotect(payload), JsonOptions)
        ?? throw new InvalidDataException("Protected job payload was empty or invalid.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed class JobEntity
{
    public long Id { get; set; }
    public string IdentityHash { get; set; } = string.Empty;
    public required byte[] Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Revision { get; set; }
}

internal sealed class JobAuditEntity
{
    public long Id { get; set; }
    public string EntityRef { get; set; } = string.Empty;
    public long PreviousRevision { get; set; }
    public long NewRevision { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class JobDbContext(DbContextOptions<JobDbContext> options) : DbContext(options)
{
    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<JobAuditEntity> JobAuditEntries => Set<JobAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.IdentityHash).IsUnique();
            entity.Property(item => item.IdentityHash).IsRequired();
            entity.Property(item => item.Payload).IsRequired();
        });
        modelBuilder.Entity<JobAuditEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.EntityRef, item.NewRevision });
            entity.Property(item => item.EntityRef).IsRequired();
            entity.Property(item => item.Operation).IsRequired();
            entity.Property(item => item.Actor).IsRequired();
        });
    }
}

internal sealed class JobDbContextFactory
{
    private readonly DbContextOptions<JobDbContext> _options;

    public JobDbContextFactory(string databasePath)
    {
        DatabasePath = databasePath;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        _options = new DbContextOptionsBuilder<JobDbContext>().UseSqlite(connectionString).Options;
    }

    public string DatabasePath { get; }
    public JobDbContext CreateDbContext() => new(_options);
}
