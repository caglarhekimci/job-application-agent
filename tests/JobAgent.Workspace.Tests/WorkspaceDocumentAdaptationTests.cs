using System.Text;
using System.Text.Json;
using JobAgent.Core.Applications;
using JobAgent.Core.Documents;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Storage;
using JobAgent.Infrastructure.Workspace;
using Xunit;

namespace JobAgent.Workspace.Tests;

public sealed class WorkspaceDocumentAdaptationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-document-adaptation-" + Guid.NewGuid());
    private LocalWorkspace Open() => new(root, Environment.CurrentDirectory, new WindowsDpapiPayloadProtector());

    [Fact]
    public async Task ExactProposalRequiresUiApprovalBeforeExportAndNeverReplacesOriginalResume()
    {
        using var workspace = Open();
        var created = await Setup(workspace);
        var before = await workspace.GetAsync();
        using var beforeExport = JsonDocument.Parse(await workspace.ExportAsync());
        var originalBytes = beforeExport.RootElement.GetProperty("resumeBytes").GetString();

        var proposed = await workspace.ProposeDocumentAdaptationFromUiAsync(new(created.Draft.Id,
            created.WorkspaceRevision, created.Draft.PayloadHash()));

        Assert.Equal(WorkspaceDocumentAdaptationStatus.Proposed, proposed.Status);
        Assert.Equal(before.Document!.Text, proposed.OriginalText);
        Assert.Contains("Professional Experience: Built payment services in C#.",
            proposed.Proposal!.Resume.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("five years", proposed.Proposal.Resume.Content, StringComparison.OrdinalIgnoreCase);
        var unapproved = await Assert.ThrowsAsync<PolicyException>(() => workspace.ExportApprovedDocumentAsync(
            created.Draft.Id, proposed.Proposal.BundleHash, AdaptedDocumentKind.Resume));
        Assert.Equal("DocumentApprovalRequired", unapproved.Code);
        var wrongHash = await Assert.ThrowsAsync<PolicyException>(() => workspace.ApproveDocumentAdaptationFromUiAsync(
            new(created.Draft.Id, proposed.Revision, new string('0', 64))));
        Assert.Equal("PackageChanged", wrongHash.Code);

        var approved = await workspace.ApproveDocumentAdaptationFromUiAsync(new(created.Draft.Id,
            proposed.Revision, proposed.Proposal.BundleHash));
        var resume = await workspace.ExportApprovedDocumentAsync(created.Draft.Id,
            approved.Proposal!.BundleHash, AdaptedDocumentKind.Resume);
        var letter = await workspace.ExportApprovedDocumentAsync(created.Draft.Id,
            approved.Proposal.BundleHash, AdaptedDocumentKind.CoverLetter);

        Assert.Equal(WorkspaceDocumentAdaptationStatus.Approved, approved.Status);
        Assert.Equal("adapted-cv.txt", resume.FileName);
        Assert.Equal("cover-letter.txt", letter.FileName);
        Assert.Equal(approved.Proposal.Resume.Content, Encoding.UTF8.GetString(resume.Bytes));
        Assert.Equal(approved.Proposal.CoverLetter.Content, Encoding.UTF8.GetString(letter.Bytes));
        var after = Assert.Single((await workspace.GetApplicationPanelAsync()).Applications);
        Assert.Equal(created.Draft.ResumeHash, after.Draft.ResumeHash);
        Assert.Equal(created.Draft.ResumeRef, after.Draft.ResumeRef);
        using var afterExport = JsonDocument.Parse(await workspace.ExportAsync());
        Assert.Equal(originalBytes, afterExport.RootElement.GetProperty("resumeBytes").GetString());
    }

    [Fact]
    public async Task ApprovedProposalBecomesStaleWhenReviewedJobBindingChanges()
    {
        using var workspace = Open();
        var created = await Setup(workspace);
        var proposed = await workspace.ProposeDocumentAdaptationFromUiAsync(new(created.Draft.Id,
            created.WorkspaceRevision, created.Draft.PayloadHash()));
        var approved = await workspace.ApproveDocumentAdaptationFromUiAsync(new(created.Draft.Id,
            proposed.Revision, proposed.Proposal!.BundleHash));
        await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = approved.Revision,
            Employer = "Synthetic Employer",
            Title = "Senior Developer",
            Text = "Build different local tools.",
            SourceUrl = "https://example.invalid/jobs/42"
        });

        var stale = await workspace.GetDocumentAdaptationAsync(created.Draft.Id);
        var error = await Assert.ThrowsAsync<PolicyException>(() => workspace.ExportApprovedDocumentAsync(
            created.Draft.Id, approved.Proposal!.BundleHash, AdaptedDocumentKind.Resume));

        Assert.Equal(WorkspaceDocumentAdaptationStatus.Stale, stale.Status);
        Assert.Equal("DocumentAdaptationStale", error.Code);
    }

    [Fact]
    public async Task ConcurrentApprovalClaimsTheSameRevisionOnlyOnce()
    {
        using var workspace = Open();
        var created = await Setup(workspace);
        var proposed = await workspace.ProposeDocumentAdaptationFromUiAsync(new(created.Draft.Id,
            created.WorkspaceRevision, created.Draft.PayloadHash()));
        var request = new WorkspaceDocumentAdaptationApproval(created.Draft.Id,
            proposed.Revision, proposed.Proposal!.BundleHash);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try
            {
                await workspace.ApproveDocumentAdaptationFromUiAsync(request);
                return "approved";
            }
            catch (InvalidOperationException)
            {
                return "conflict";
            }
        }));

        Assert.Equal(1, outcomes.Count(outcome => outcome == "approved"));
        Assert.Equal(1, outcomes.Count(outcome => outcome == "conflict"));
    }

    [Fact]
    public async Task ReplacingResumeStalesProposalButKeepsItsOriginalSourcePreview()
    {
        using var workspace = Open();
        var created = await Setup(workspace);
        var proposed = await workspace.ProposeDocumentAdaptationFromUiAsync(new(created.Draft.Id,
            created.WorkspaceRevision, created.Draft.PayloadHash()));
        var originalText = proposed.OriginalText;

        await workspace.ImportAsync(new MemoryStream("Replacement resume text"u8.ToArray()), "replacement.txt");
        var stale = await workspace.GetDocumentAdaptationAsync(created.Draft.Id);

        Assert.Equal(WorkspaceDocumentAdaptationStatus.Stale, stale.Status);
        Assert.Equal(originalText, stale.OriginalText);
        Assert.DoesNotContain("Replacement resume text", stale.OriginalText, StringComparison.Ordinal);
    }

    private static async Task<WorkspaceApplicationView> Setup(LocalWorkspace workspace)
    {
        var imported = await workspace.ImportAsync(new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n',
            "Synthetic User", "Professional Experience: Built payment services in C#.",
            "Personal Project: Created a weather application."))), "synthetic.txt");
        var profile = await workspace.ReviewProfileAsync(new()
        {
            ExpectedRevision = imported.Revision,
            FullName = "Synthetic User",
            Email = "synthetic@example.invalid",
            Experience =
            [
                new ReviewedExperience
                {
                    SourceSpan = "line 2", Start = new DateOnly(2020, 1, 1), End = new DateOnly(2024, 1, 1),
                    Role = "Developer", Kind = ExperienceKind.Professional, Skills = ["C#"]
                },
                new ReviewedExperience
                {
                    SourceSpan = "line 3", Start = new DateOnly(2019, 1, 1), End = new DateOnly(2019, 12, 1),
                    Role = "Maker", Kind = ExperienceKind.PersonalProject, Skills = ["TypeScript"]
                }
            ]
        });
        var job = await workspace.ReviewJobAsync(new()
        {
            ExpectedRevision = profile.Revision,
            Employer = "Synthetic Employer",
            Title = "Developer",
            Text = "Build local tools with C#.",
            SourceUrl = "https://example.invalid/jobs/42",
            Requirements =
            [
                new JobRequirement
                {
                    Id = "csharp", RequirementText = "C#", Type = RequirementType.Skill,
                    Importance = RequirementImportance.Mandatory, Skill = "C#"
                }
            ]
        });
        return await workspace.CreateApplicationFromUiAsync(new(job.Revision, null, "dotnet-developer"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
