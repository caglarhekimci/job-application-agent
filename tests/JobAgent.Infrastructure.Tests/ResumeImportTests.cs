using System.Text;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Documents;

namespace JobAgent.Infrastructure.Tests;

public sealed class ResumeImportTests
{
    private readonly ResumeImporter _importer = new();

    [Fact]
    public async Task PlainTextResume_ProducesReviewableFacts()
    {
        const string text = "Synthetic Resume\nProfessional Experience: C# - 3 years";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var result = await _importer.ImportTextAsync(stream, "resume.txt");

        var fact = Assert.Single(result.ProposedFacts);
        Assert.Equal(VerificationStatus.Proposed, fact.VerificationStatus);
        Assert.Equal("professional-experience", fact.Kind);
        Assert.Equal("line 2", fact.SourceSpan);
        Assert.False(string.IsNullOrWhiteSpace(result.DocumentId));
    }

    [Fact]
    public async Task CorruptDocument_IsRejected()
    {
        await using var stream = new MemoryStream([0xC3, 0x28]);

        await Assert.ThrowsAsync<InvalidDataException>(() => _importer.ImportTextAsync(stream, "resume.txt"));
    }

    [Fact]
    public async Task OversizedDocument_IsRejected()
    {
        await using var stream = new MemoryStream(new byte[ResumeImporter.MaximumBytes + 1]);

        await Assert.ThrowsAsync<InvalidDataException>(() => _importer.ImportTextAsync(stream, "resume.txt"));
    }

    [Fact]
    public async Task PersonalProject_IsNotEmployment()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Personal Project: Java game"));

        var result = await _importer.ImportTextAsync(stream, "resume.txt");

        var fact = Assert.Single(result.ProposedFacts);
        Assert.Equal("personal-project", fact.Kind);
        Assert.DoesNotContain("employment", fact.Kind, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonTextDocument_IsRejectedUntilSafeParserExists()
    {
        await using var stream = new MemoryStream("fake"u8.ToArray());

        await Assert.ThrowsAsync<NotSupportedException>(() => _importer.ImportTextAsync(stream, "resume.docm"));
    }
}
