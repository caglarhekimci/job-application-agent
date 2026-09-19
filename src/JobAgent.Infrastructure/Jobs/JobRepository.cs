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
        await using var context = _factory.CreateDbContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async Task SaveAsync(JobPosting job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        Validate(job);
        var identityHash = Hash(job.DuplicateKey);
        var payload = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(job, JsonOptions));
        var now = _timeProvider.GetUtcNow();
        await using var context = _factory.CreateDbContext();
        var existing = await context.Jobs.SingleOrDefaultAsync(item => item.IdentityHash == identityHash,
            cancellationToken);
        if (existing is null)
        {
            context.Jobs.Add(new()
            {
                IdentityHash = identityHash,
                Payload = payload,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Payload = payload;
            existing.UpdatedAt = now;
        }
        await context.SaveChangesAsync(cancellationToken);
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
}

internal sealed class JobDbContext(DbContextOptions<JobDbContext> options) : DbContext(options)
{
    public DbSet<JobEntity> Jobs => Set<JobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.IdentityHash).IsUnique();
            entity.Property(item => item.IdentityHash).IsRequired();
            entity.Property(item => item.Payload).IsRequired();
        });
    }
}

internal sealed class JobDbContextFactory
{
    private readonly DbContextOptions<JobDbContext> _options;

    public JobDbContextFactory(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString();
        _options = new DbContextOptionsBuilder<JobDbContext>().UseSqlite(connectionString).Options;
    }

    public JobDbContext CreateDbContext() => new(_options);
}
