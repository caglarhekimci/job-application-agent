using System.Data;
using System.Text.Json;
using JobAgent.Core.Profiles;
using Microsoft.EntityFrameworkCore;

namespace JobAgent.Infrastructure.Storage;

public sealed record ProfileStoreOptions
{
    public required string DatabasePath { get; init; }
    public required string CheckoutRoot { get; init; }
    public bool AllowSyntheticPlaintextForLinuxTests { get; init; }
}

public sealed class ProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly FoundationDbContextFactory _factory;
    private readonly TimeProvider _timeProvider;

    public ProfileRepository(ProfileStoreOptions options, IPayloadProtector protector,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(protector);
        Options = options;
        Protector = protector;

        var databasePath = Path.GetFullPath(options.DatabasePath);
        var checkoutRoot = Path.GetFullPath(options.CheckoutRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (databasePath.StartsWith(checkoutRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The profile database must be stored outside the source checkout.");
        if (protector.IsPlaintext && (OperatingSystem.IsWindows() || !options.AllowSyntheticPlaintextForLinuxTests))
            throw new InvalidOperationException("Plaintext protection is limited to explicitly enabled synthetic Linux tests.");

        _factory = new(databasePath);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public ProfileStoreOptions Options { get; }
    public IPayloadProtector Protector { get; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(Options.DatabasePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        await ProfileSchema.InitializeAsync(_factory.DatabasePath, cancellationToken);
    }

    public async Task SaveInitialAsync(CandidateProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsurePlaintextIsSynthetic(profile);
        if (profile.Version < 1)
            throw new ArgumentException("Profile version must be positive.", nameof(profile));

        await using var context = _factory.CreateDbContext();
        if (await context.ProfileRevisions.AnyAsync(item => item.ProfileId == profile.Id, cancellationToken))
            throw new InvalidOperationException("A profile revision already exists.");
        context.ProfileRevisions.Add(ToEntity(profile));
        context.ProfileAuditEntries.Add(Audit(profile.Id, profile.Version, "ProfileCreated"));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<CandidateProfile?> GetLatestAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateDbContext();
        var entity = await context.ProfileRevisions.AsNoTracking()
            .Where(item => item.ProfileId == profileId)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Deserialize<CandidateProfile>(entity.Payload);
    }

    public async Task SavePendingPatchAsync(ProfilePatch patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patch);
        EnsurePlaintextIsSynthetic(patch.ProposedProfile);
        await using var context = _factory.CreateDbContext();
        if (await context.PendingPatches.AnyAsync(item => item.PatchId == patch.Id, cancellationToken))
            return;
        context.PendingPatches.Add(new()
        {
            PatchId = patch.Id,
            ProfileId = patch.ProfileId,
            BaseVersion = patch.BaseVersion,
            Payload = Serialize(patch),
            Status = "Pending",
            ProposedAt = patch.ProposedAt
        });
        context.ProfileAuditEntries.Add(Audit(patch.ProfileId, patch.BaseVersion,
            "PatchProposed", patch.Id));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProfilePatchResult> ApplyPatchAsync(ProfilePatch patch, bool locallyApproved, CancellationToken cancellationToken = default)
    {
        await SavePendingPatchAsync(patch, cancellationToken);
        if (!locallyApproved)
            return new() { Applied = false, Error = "Local user approval is required." };

        await using var context = _factory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var currentEntity = await context.ProfileRevisions
            .Where(item => item.ProfileId == patch.ProfileId)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentEntity is null)
            return new() { Applied = false, Error = "Profile was not found." };
        if (currentEntity.Version != patch.BaseVersion)
            return new() { Applied = false, Error = $"Profile version conflict: expected {patch.BaseVersion}, current {currentEntity.Version}." };

        var current = Deserialize<CandidateProfile>(currentEntity.Payload);
        var existingVerified = current.Facts
            .Where(fact => fact.VerificationStatus == VerificationStatus.Verified)
            .ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        var facts = patch.ProposedProfile.Facts
            .Select(fact => fact.VerificationStatus == VerificationStatus.Verified &&
                (!existingVerified.TryGetValue(fact.Id, out var prior) || prior != fact)
                    ? fact with { VerificationStatus = VerificationStatus.Proposed }
                    : fact)
            .ToList();
        var next = patch.ProposedProfile with
        {
            Id = current.Id,
            Version = current.Version + 1,
            Facts = facts
        };
        EnsurePlaintextIsSynthetic(next);
        context.ProfileRevisions.Add(ToEntity(next));
        var pending = await context.PendingPatches.SingleAsync(item => item.PatchId == patch.Id, cancellationToken);
        pending.Status = "Applied";
        context.ProfileAuditEntries.Add(Audit(next.Id, next.Version, "PatchApplied", patch.Id));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new() { Applied = false, Error = "Profile version conflict while saving." };
        }

        return new() { Applied = true, Profile = next };
    }

    public async Task<string?> ExportAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await GetLatestAsync(profileId, cancellationToken);
        return profile is null ? null : JsonSerializer.Serialize(profile, JsonOptions);
    }

    public async Task DeleteAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var context = _factory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await context.ProfileRevisions.Where(item => item.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await context.PendingPatches.Where(item => item.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await context.ProfileAuditEntries.Where(item => item.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private ProfileRevisionEntity ToEntity(CandidateProfile profile) => new()
    {
        ProfileId = profile.Id,
        Version = profile.Version,
        Payload = Serialize(profile),
        CreatedAt = _timeProvider.GetUtcNow()
    };

    private ProfileAuditEntity Audit(Guid profileId, int profileVersion, string operation,
        Guid? correlationId = null) => new()
        {
            ProfileId = profileId,
            ProfileVersion = profileVersion,
            Operation = operation,
            CorrelationId = correlationId,
            OccurredAt = _timeProvider.GetUtcNow()
        };

    private byte[] Serialize<T>(T value) => Protector.Protect(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions));

    private T Deserialize<T>(byte[] payload) =>
        JsonSerializer.Deserialize<T>(Protector.Unprotect(payload), JsonOptions)
        ?? throw new InvalidDataException("Protected profile payload was empty or invalid.");

    private void EnsurePlaintextIsSynthetic(CandidateProfile profile)
    {
        if (Protector.IsPlaintext && !profile.Synthetic)
            throw new InvalidOperationException("Plaintext test protection accepts synthetic profiles only.");
    }
}
