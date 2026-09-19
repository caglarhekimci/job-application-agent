using System.Text;
using JobAgent.Core.Answers;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Xunit;

namespace JobAgent.Workspace.Tests;

public sealed class WorkspaceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-workspace-" + Guid.NewGuid());
    private LocalWorkspace Open() => new(root, Environment.CurrentDirectory, new WindowsDpapiPayloadProtector());
    private static MemoryStream Resume() => new(Encoding.UTF8.GetBytes("Synthetic User\nProfessional Experience: C# developer 2020-2024"));

    [Fact]
    public async Task ImportStaysUnverifiedUntilExplicitVersionBoundReview()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        Assert.Null(imported.Profile.VerifiedAt);
        var before = await workspace.ResolveAsync(new() { Key = "contact.name" });
        Assert.Equal(AnswerStatus.RequiresReview, before.Status);
        var reviewed = await workspace.ReviewProfileAsync(Review(imported.Revision));
        Assert.Equal("Synthetic User", reviewed.Profile.FullName);
        Assert.NotNull(reviewed.Profile.VerifiedAt);
        Assert.Equal(AnswerStatus.Resolved, (await workspace.ResolveAsync(new() { Key = "contact.name" })).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ReviewProfileAsync(Review(imported.Revision)));
    }

    [Fact]
    public async Task ProtectedRestartPreservesDocumentAndReviewedProfile()
    {
        using (var workspace = Open())
        {
            var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
            await workspace.ReviewProfileAsync(Review(imported.Revision));
        }
        using var reopened = Open();
        var state = await reopened.GetAsync();
        Assert.Equal("Synthetic User", state.Profile.FullName);
        Assert.Contains("C# developer", state.Document!.Text);
        foreach (var file in Directory.GetFiles(root))
            Assert.DoesNotContain("Synthetic User", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)));
    }

    [Fact]
    public async Task ChangedDocumentInvalidatesConfirmationsAndExistingJobAnswers()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        await workspace.ReviewProfileAsync(Review(imported.Revision));
        var next = await workspace.ImportAsync(new MemoryStream("Different document"u8.ToArray()), "updated.txt");
        Assert.Null(next.Profile.VerifiedAt);
        Assert.Empty(next.Profile.LocalConfirmations);
        Assert.Empty(next.Profile.Experience);
        Assert.Equal(AnswerStatus.RequiresReview, (await workspace.ResolveAsync(new() { Key = "contact.email" })).Status);
    }

    [Fact]
    public async Task FabricatedEvidenceOrInvalidDatesCannotBeConfirmed()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        var review = Review(imported.Revision) with
        {
            Experience = [new() {
            SourceSpan = "line 999", Start = new(2020, 1, 1), End = new(2024, 1, 1), Role = "Developer", Skills = ["C#"] }]
        };
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.ReviewProfileAsync(review));
        var invalidDate = review with { Experience = [review.Experience[0] with { SourceSpan = "line 2", Start = new(2025, 1, 1) }] };
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.ReviewProfileAsync(invalidDate));
        Assert.Null((await workspace.GetAsync()).Profile.VerifiedAt);
    }

    [Fact]
    public async Task PrivateMinimumNeverBecomesAnAnswerAndDeleteRemovesProfileAndResume()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        var reviewed = await workspace.ReviewProfileAsync(Review(imported.Revision));
        Assert.Equal("100000", (await workspace.ResolveAsync(new() { Key = "salary.expected.monthly.net.TRY" })).Value);
        Assert.Equal(AnswerStatus.NeedsInput, (await workspace.ResolveAsync(new() { Key = "salary.current" })).Status);
        var export = await workspace.ExportAsync();
        Assert.Contains("Synthetic User", export);
        await workspace.DeleteAsync(reviewed.Revision);
        Assert.Null((await workspace.GetAsync()).Document);
        Assert.Empty((await workspace.GetAsync()).Profile.FullName);
        using var reopened = Open();
        Assert.Null((await reopened.GetAsync()).Document);
    }

    [Fact]
    public async Task OptionalPrivateMinimumRetainsWhetherUserSuppliedIt()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        var reviewed = await workspace.ReviewProfileAsync(Review(imported.Revision) with { SalaryTarget = null });
        Assert.Equal(85000m, reviewed.SalaryPrivateMinimum);
        using var reopened = Open();
        Assert.Equal(85000m, (await reopened.GetAsync()).SalaryPrivateMinimum);
        var unspecified = await workspace.ReviewProfileAsync(Review(reviewed.Revision) with { SalaryPrivateMinimum = null });
        Assert.Null(unspecified.SalaryPrivateMinimum);
    }

    [Fact]
    public void RuntimeInsideCheckoutAndPlaintextProtectionAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new LocalWorkspace(Path.Combine(Environment.CurrentDirectory, "private"), Environment.CurrentDirectory, new WindowsDpapiPayloadProtector()));
        Assert.Throws<InvalidOperationException>(() => new LocalWorkspace(root, Environment.CurrentDirectory, new SyntheticPlaintextPayloadProtector()));
    }

    [Fact]
    public async Task ReviewedMemoryIsBoundToCurrentJobPersistsAndCanBeRevoked()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        var profile = await workspace.ReviewProfileAsync(Review(imported.Revision));
        var job = await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = profile.Revision,
            Employer = "First employer",
            Title = "Developer",
            Text = "A synthetic role."
        });
        var request = new WorkspaceAnswerReview
        {
            ExpectedRevision = job.Revision,
            SemanticKey = "motivation",
            Answer = "I prefer this role's focus.",
            Scope = AnswerScopeType.Company
        };
        var saved = await workspace.ReviewAnswerAsync(request);
        Assert.Equal("First employer", Assert.Single(saved.Profile.Answers).ScopeId);
        Assert.Equal(profile.Profile.Version + 1, saved.Profile.Version);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ReviewAnswerAsync(request));
        using var reopened = Open();
        var answer = await reopened.ResolveAsync(new() { Key = "motivation", Language = "tr" });
        Assert.Equal("I prefer this role's focus.", answer.Value);
        var other = await reopened.ReviewJobAsync(new()
        {
            ExpectedRevision = saved.Revision,
            Employer = "Other employer",
            Title = "Other",
            Text = "Another synthetic role."
        });
        Assert.Equal(AnswerStatus.NeedsInput, (await reopened.ResolveAsync(new() { Key = "motivation", Language = "tr" })).Status);
        var revoked = await reopened.RevokeAnswerAsync(new()
        {
            ExpectedRevision = other.Revision,
            Key = new() { SemanticKey = "motivation", Scope = AnswerScopeType.Company, ScopeId = "First employer", Language = "tr" }
        });
        Assert.Empty(revoked.Profile.Answers);
        using var export = System.Text.Json.JsonDocument.Parse(await reopened.ExportAsync());
        Assert.Contains(export.RootElement.GetProperty("previousVersions").EnumerateArray(),
            version => version.GetProperty("answers").EnumerateArray().Any(a =>
                a.GetProperty("answer").GetString() == "I prefer this role's focus."));
    }

    [Fact]
    public async Task ScopedMemoryRequiresReviewedProfileAndAJobAndInvalidatesOnCvChange()
    {
        using var workspace = Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ReviewAnswerAsync(new()
        { SemanticKey = "availability", Answer = "Two weeks.", Scope = AnswerScopeType.Default }));
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        var profile = await workspace.ReviewProfileAsync(Review(imported.Revision));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ReviewAnswerAsync(new()
        { ExpectedRevision = profile.Revision, SemanticKey = "availability", Answer = "Two weeks.", Scope = AnswerScopeType.Application }));
        await workspace.ReviewAnswerAsync(new()
        {
            ExpectedRevision = profile.Revision,
            SemanticKey = "availability",
            Answer = "Two weeks.",
            Scope = AnswerScopeType.Default
        });
        var changed = await workspace.ImportAsync(Resume(), "changed.txt");
        Assert.Empty(changed.Profile.Answers);
    }

    [Fact]
    public async Task SamePostingReviewPreservesApplicationMemoryButChangedEmployerDoesNot()
    {
        using var workspace = Open();
        var imported = await workspace.ImportAsync(Resume(), "candidate.txt");
        var profile = await workspace.ReviewProfileAsync(Review(imported.Revision));
        var request = new JobReview
        {
            ExpectedRevision = profile.Revision,
            Employer = "First",
            Title = "Developer",
            Text = "Synthetic role.",
            SourceUrl = "https://example.invalid/jobs/1"
        };
        var job = await workspace.ReviewJobAsync(request);
        var memory = await workspace.ReviewAnswerAsync(new()
        {
            ExpectedRevision = job.Revision,
            SemanticKey = "motivation",
            Answer = "A specific answer.",
            Scope = AnswerScopeType.Application
        });
        var reviewed = await workspace.ReviewJobAsync(request with { ExpectedRevision = memory.Revision });
        Assert.Equal(job.Job!.Id, reviewed.Job!.Id);
        Assert.Equal("A specific answer.", (await workspace.ResolveAsync(new() { Key = "motivation", Language = "tr" })).Value);
        var other = await workspace.ReviewJobAsync(request with { ExpectedRevision = reviewed.Revision, Employer = "Other" });
        Assert.NotEqual(job.Job.Id, other.Job!.Id);
        Assert.Equal(AnswerStatus.NeedsInput, (await workspace.ResolveAsync(new() { Key = "motivation", Language = "tr" })).Status);
    }

    private static ProfileReview Review(long revision) => new()
    {
        ExpectedRevision = revision,
        FullName = "Synthetic User",
        Email = "synthetic@example.invalid",
        SalaryTarget = 100000,
        SalaryPrivateMinimum = 85000,
        Experience = [new() { SourceSpan = "line 2", Start = new(2020, 1, 1), End = new(2024, 1, 1),
            Role = "Developer", Kind = ExperienceKind.Professional, Skills = ["C#"] }]
    };

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
