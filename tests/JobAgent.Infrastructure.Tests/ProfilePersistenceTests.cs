using System.Text;
using JobAgent.Core;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;

namespace JobAgent.Infrastructure.Tests;

public sealed class ProfilePersistenceTests
{
    [Fact]
    public async Task VerifiedProfile_SurvivesRestart()
    {
        using var temp = new TemporaryStore();
        var protector = CreateProtector();
        var first = new ProfileRepository(temp.Options(!OperatingSystem.IsWindows()), protector);
        await first.InitializeAsync();
        await first.SaveInitialAsync(SyntheticData.Profile());

        var restarted = new ProfileRepository(temp.Options(!OperatingSystem.IsWindows()), protector);
        await restarted.InitializeAsync();
        var loaded = await restarted.GetLatestAsync(SyntheticData.Profile().Id);

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Version);
        Assert.Equal("candidate@example.invalid", loaded.Email);
        Assert.Equal(VerificationStatus.Verified, Assert.Single(loaded.Facts).VerificationStatus);
    }

    [Fact]
    public async Task StalePatch_IsRejected()
    {
        using var temp = new TemporaryStore();
        var repository = new ProfileRepository(temp.Options(!OperatingSystem.IsWindows()), CreateProtector());
        await repository.InitializeAsync();
        var original = SyntheticData.Profile();
        await repository.SaveInitialAsync(original);
        var first = Patch(original, "First");
        var stale = Patch(original, "Stale");

        var applied = await repository.ApplyPatchAsync(first, locallyApproved: true);
        var rejected = await repository.ApplyPatchAsync(stale, locallyApproved: true);

        Assert.True(applied.Applied);
        Assert.Equal(2, applied.Profile?.Version);
        Assert.False(rejected.Applied);
        Assert.Contains("version", rejected.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("First", (await repository.GetLatestAsync(original.Id))?.FullName);
    }

    [Fact]
    public async Task UnapprovedPatch_RemainsPendingAndDoesNotChangeProfile()
    {
        using var temp = new TemporaryStore();
        var repository = new ProfileRepository(temp.Options(!OperatingSystem.IsWindows()), CreateProtector());
        await repository.InitializeAsync();
        var original = SyntheticData.Profile();
        await repository.SaveInitialAsync(original);

        var result = await repository.ApplyPatchAsync(Patch(original, "Unapproved"), locallyApproved: false);

        Assert.False(result.Applied);
        Assert.Equal(original.FullName, (await repository.GetLatestAsync(original.Id))?.FullName);
    }

    [Fact]
    public async Task EditedFactWithSameId_DoesNotInheritVerification()
    {
        using var temp = new TemporaryStore();
        var repository = new ProfileRepository(temp.Options(!OperatingSystem.IsWindows()), CreateProtector());
        await repository.InitializeAsync();
        var original = SyntheticData.Profile();
        await repository.SaveInitialAsync(original);
        var changedFact = original.Facts[0] with { Value = "Rust" };
        var patch = new ProfilePatch
        {
            ProfileId = original.Id,
            BaseVersion = original.Version,
            ProposedAt = DateTimeOffset.UtcNow,
            ProposedProfile = original with { Facts = [changedFact] }
        };

        var result = await repository.ApplyPatchAsync(patch, locallyApproved: true);

        Assert.Equal(VerificationStatus.Proposed, Assert.Single(result.Profile?.Facts!).VerificationStatus);
    }

    [Fact]
    public async Task ProtectedDatabase_DoesNotContainProfileEmail()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var temp = new TemporaryStore();
        var repository = new ProfileRepository(temp.Options(), new WindowsDpapiPayloadProtector());
        await repository.InitializeAsync();
        await repository.SaveInitialAsync(SyntheticData.Profile());

        var bytes = await File.ReadAllBytesAsync(temp.DatabasePath);
        var databaseText = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("candidate@example.invalid", databaseText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAndDelete_AreAvailableToTheUser()
    {
        using var temp = new TemporaryStore();
        var repository = new ProfileRepository(temp.Options(!OperatingSystem.IsWindows()), CreateProtector());
        await repository.InitializeAsync();
        var profile = SyntheticData.Profile();
        await repository.SaveInitialAsync(profile);

        var exported = await repository.ExportAsync(profile.Id);
        await repository.DeleteAsync(profile.Id);

        Assert.Contains("candidate@example.invalid", exported, StringComparison.Ordinal);
        Assert.Null(await repository.GetLatestAsync(profile.Id));
    }

    [Fact]
    public void RepositoryPathInsideCheckout_IsRejected()
    {
        var path = Path.Combine(Environment.CurrentDirectory, "private", "profiles.db");

        Assert.Throws<InvalidOperationException>(() => new ProfileRepository(new()
        {
            DatabasePath = path,
            CheckoutRoot = Environment.CurrentDirectory
        }, CreateProtector()));
    }

    private static IPayloadProtector CreateProtector() => OperatingSystem.IsWindows()
        ? new WindowsDpapiPayloadProtector()
        : new SyntheticPlaintextPayloadProtector();

    private static ProfilePatch Patch(CandidateProfile original, string name) => new()
    {
        ProfileId = original.Id,
        BaseVersion = original.Version,
        ProposedAt = DateTimeOffset.UtcNow,
        ProposedProfile = original with { FullName = name }
    };
}
