using JobAgent.Core.Jobs;
using JobAgent.Core.Permissions;

namespace JobAgent.Core.Tests;

public sealed class SourcePermissionTests
{
    [Fact]
    public void LinkedInWithoutPermission_IsBlocked()
    {
        var permission = SourcePermission.LinkedInBlocked();

        Assert.False(permission.Allows(SourceAction.ReadPage, TestProfiles.Now));
        Assert.False(permission.Allows(SourceAction.Autofill, TestProfiles.Now));
        Assert.False(permission.Allows(SourceAction.Submit, TestProfiles.Now));
    }

    [Fact]
    public void ImportedUrl_IsNotFetchedAutomatically()
    {
        var posting = JobPostingImporter.ImportProvidedText("id", "Employer", "Engineer", "Provided text", "https://example.invalid/job/1");

        Assert.Equal("Provided text", posting.Text);
        Assert.Equal("https://example.invalid/job/1", posting.SourceUrl);
        Assert.True(posting.SourcePermission.Allows(SourceAction.AnalyzeProvidedText, TestProfiles.Now));
        Assert.False(posting.SourcePermission.Allows(SourceAction.Fetch, TestProfiles.Now));
    }

    [Fact]
    public void ExpiringNow_IsDenied()
    {
        var permission = SourcePermission.ForAuthorizedSource(SourceKind.AuthorizedCareerSite,
            [SourceAction.ReadPage], "evidence-1", "job-read", "https://careers.example.invalid",
            TestProfiles.Now.AddDays(-1), TestProfiles.Now);

        Assert.False(permission.Allows(SourceAction.ReadPage,
            "https://careers.example.invalid", TestProfiles.Now));
    }

    [Fact]
    public void ExternalAction_RequiresMatchingRecipientOrigin()
    {
        var permission = SourcePermission.ForAuthorizedSource(SourceKind.AuthorizedCareerSite,
            [SourceAction.ReadPage], "evidence-1", "job-read", "https://careers.example.invalid",
            TestProfiles.Now.AddDays(-1), TestProfiles.Now.AddDays(1));

        Assert.True(permission.Allows(SourceAction.ReadPage,
            "https://careers.example.invalid", TestProfiles.Now));
        Assert.False(permission.Allows(SourceAction.ReadPage,
            "https://other.example.invalid", TestProfiles.Now));
        Assert.False(permission.Allows(SourceAction.Submit,
            "https://careers.example.invalid", TestProfiles.Now));
    }

    [Fact]
    public void ArbitraryLinkedInAllowedShape_RemainsBlocked()
    {
        var permission = new SourcePermission
        {
            Source = SourceKind.LinkedInRestricted,
            Status = PermissionStatus.Allowed,
            AllowedActions = new HashSet<SourceAction> { SourceAction.Search, SourceAction.ReadPage,
                SourceAction.Autofill, SourceAction.Submit },
            EvidenceId = "caller-claimed",
            Scope = "all",
            RecipientOrigin = "https://www.linkedin.com",
            VerifiedAt = TestProfiles.Now.AddDays(-1),
            ExpiresAt = TestProfiles.Now.AddDays(1)
        };

        Assert.False(permission.Allows(SourceAction.ReadPage,
            "https://www.linkedin.com", TestProfiles.Now));
        Assert.False(permission.Allows(SourceAction.Submit,
            "https://www.linkedin.com", TestProfiles.Now));
        Assert.Throws<InvalidDataException>(() => SourcePermissionPolicy.ValidateForPersistence(permission,
            TestProfiles.Now));
    }
}
