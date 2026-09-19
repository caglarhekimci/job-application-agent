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
