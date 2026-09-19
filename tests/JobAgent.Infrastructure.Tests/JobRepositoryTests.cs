using System.Text;
using JobAgent.Core.Jobs;
using JobAgent.Core.Permissions;
using JobAgent.Infrastructure.Jobs;
using JobAgent.Infrastructure.Storage;

namespace JobAgent.Infrastructure.Tests;

public sealed class JobRepositoryTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 19, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtectedJobAndPermissionMetadata_SurviveRestart()
    {
        using var temp = new TemporaryStore();
        var protector = CreateProtector();
        var first = Repository(temp, protector);
        await first.InitializeAsync();
        var job = ReviewedJob("job-1");

        await first.SaveAsync(job);
        var restarted = Repository(temp, protector);
        await restarted.InitializeAsync();
        var loaded = await restarted.GetAsync(job.DuplicateKey);

        Assert.NotNull(loaded);
        Assert.Equal(job.CanonicalUrl, loaded.CanonicalUrl);
        Assert.Equal("user-provided-text", loaded.SourcePermission.EvidenceId);
        Assert.Equal(RequirementReviewStatus.Confirmed, Assert.Single(loaded.Requirements).ReviewStatus);
        if (OperatingSystem.IsWindows())
        {
            var databaseText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(JobPath(temp)));
            Assert.DoesNotContain(job.Employer, databaseText, StringComparison.Ordinal);
            Assert.DoesNotContain(job.CanonicalUrl, databaseText, StringComparison.Ordinal);
            Assert.DoesNotContain(job.SourcePermission.EvidenceId, databaseText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task SameCompanyDifferentExternalIds_PersistSeparately()
    {
        using var temp = new TemporaryStore();
        var repository = Repository(temp, CreateProtector());
        await repository.InitializeAsync();

        await repository.SaveAsync(ReviewedJob("job-1"));
        await repository.SaveAsync(ReviewedJob("job-2"));

        Assert.Equal(2, (await repository.ListAsync()).Count);
    }

    [Fact]
    public async Task SavingSameIdentity_UpdatesInsteadOfDuplicating()
    {
        using var temp = new TemporaryStore();
        var repository = Repository(temp, CreateProtector());
        await repository.InitializeAsync();
        var original = ReviewedJob("job-1");
        await repository.SaveAsync(original);

        await repository.SaveAsync(original with { Title = "Updated synthetic title", LastCheckedAt = FixedNow.AddHours(1) });

        var stored = Assert.Single(await repository.ListAsync());
        Assert.Equal("Updated synthetic title", stored.Title);
        Assert.Equal(FixedNow.AddHours(1), stored.LastCheckedAt);
    }

    [Fact]
    public async Task ArbitraryLinkedInPermission_IsRejectedBeforePersistence()
    {
        using var temp = new TemporaryStore();
        var repository = Repository(temp, CreateProtector());
        await repository.InitializeAsync();
        var job = ReviewedJob("job-1") with
        {
            SourcePermission = new()
            {
                Source = SourceKind.LinkedInRestricted,
                Status = PermissionStatus.Allowed,
                AllowedActions = new HashSet<SourceAction> { SourceAction.ReadPage },
                EvidenceId = "untrusted",
                Scope = "read",
                RecipientOrigin = "https://www.linkedin.com",
                VerifiedAt = FixedNow.AddDays(-1),
                ExpiresAt = FixedNow.AddDays(1)
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(job));
    }

    [Fact]
    public void JobsDatabaseInsideCheckout_IsRejected()
    {
        var path = Path.Combine(Environment.CurrentDirectory, "private", "jobs.db");

        Assert.Throws<InvalidOperationException>(() => new JobRepository(new()
        {
            DatabasePath = path,
            CheckoutRoot = Environment.CurrentDirectory
        }, CreateProtector()));
    }

    private static JobPosting ReviewedJob(string id)
    {
        var proposal = JobPostingImporter.ProposeProvidedText(id, "Synthetic Employer", "C# Engineer",
            "Mandatory: 3 years of professional C# experience.",
            $"https://careers.example.invalid/jobs/apply?jobId={id}&utm_source=test", FixedNow,
            synthetic: true);
        return JobPostingImporter.ConfirmRequirements(proposal,
            proposal.SuggestedRequirements.Select(item => item.Id), FixedNow);
    }

    private static string JobPath(TemporaryStore temp) => Path.Combine(temp.DirectoryPath, "jobs.db");

    private static JobRepository Repository(TemporaryStore temp, IPayloadProtector protector) => new(new()
    {
        DatabasePath = JobPath(temp),
        CheckoutRoot = Environment.CurrentDirectory,
        AllowSyntheticPlaintextForLinuxTests = !OperatingSystem.IsWindows()
    }, protector, new FixedTimeProvider(FixedNow));

    private static IPayloadProtector CreateProtector() => OperatingSystem.IsWindows()
        ? new WindowsDpapiPayloadProtector()
        : new SyntheticPlaintextPayloadProtector();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
