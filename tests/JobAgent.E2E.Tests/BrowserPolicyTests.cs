using System.Text;
using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace JobAgent.E2E.Tests;

public sealed class BrowserPolicyTests
{
    internal static readonly ResumeDocument Resume = new("synthetic-resume", "synthetic-resume.txt",
        Encoding.UTF8.GetBytes("SYNTHETIC CV\nSynthetic Candidate\n"));
    internal static ApplicationDraft Draft(string origin) => new()
    {
        ProfileId = Guid.NewGuid(),
        ProfileVersion = 1,
        JobKey = "synthetic-dotnet",
        JobTitle = ".NET Developer",
        Employer = "Synthetic Labs",
        Synthetic = true,
        RecipientOrigin = origin,
        ResumeHash = Resume.Hash,
        ResumeRef = Resume.Reference,
        Answers = new()
        {
            ["contact.name"] = "Synthetic Candidate",
            ["contact.email"] = "candidate@example.invalid",
            ["salary.expected.monthly.net.TRY"] = "100000",
            ["experience.professional.csharp.years"] = "3"
        }
    };
    internal static ApprovalReceipt Approve(ApplicationDraft draft, ApprovalPurpose purpose) =>
        ApprovalPolicy.GrantFromUserInterface(draft, purpose, "synthetic-user-session", DateTimeOffset.UtcNow);

    [Fact]
    public async Task NoSharingConsent_DoesNotTouchSite()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(Draft(site.Urls.Single()), null, Resume));
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    [Fact]
    public async Task DisallowedOrigin_IsNeverNavigated()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        var draft = Draft("https://www.linkedin.com");
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft, Approve(draft, ApprovalPurpose.ShareData), Resume));
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    [Fact]
    public async Task UploadUsesApprovedFileHash()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        var draft = Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            Approve(draft, ApprovalPurpose.ShareData), Resume with { Bytes = Encoding.UTF8.GetBytes("other CV") }));
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    [Fact]
    public async Task CancelStopsNextAction()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        var draft = Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => browser.PrepareAsync(draft,
            Approve(draft, ApprovalPurpose.ShareData), Resume, new CancellationToken(true)));
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    [Theory]
    [InlineData(ManualChallengeKind.Captcha)]
    [InlineData(ManualChallengeKind.Mfa)]
    public async Task ManualChallenge_PausesBeforeAnySubmission(ManualChallengeKind challenge)
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ManualChallenge: challenge));
        await site.StartAsync();
        var draft = Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));

        var denied = await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            Approve(draft, ApprovalPurpose.ShareData), Resume));

        Assert.Equal("ManualTakeoverRequired", denied.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Fact]
    public async Task ChallengeAppearingAfterPreparation_IsCaughtImmediatelyBeforeSubmit()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0",
            new(ManualChallenge: ManualChallengeKind.Mfa, ManualChallengeAfterResumeMilliseconds: 750));
        await site.StartAsync();
        var draft = Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await browser.PrepareAsync(draft, Approve(draft, ApprovalPurpose.ShareData), Resume);
        await Task.Delay(1000);

        var denied = await Assert.ThrowsAsync<PolicyException>(() => browser.SubmitAsync(draft,
            Approve(draft, ApprovalPurpose.Submit)));

        Assert.Equal("ManualTakeoverRequired", denied.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Fact]
    public async Task ManagedForm_RequiresSeparateSubmissionApproval_ThenVerifiesReceipt()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0");
        await site.StartAsync();
        var draft = Draft(site.Urls.Single());
        await using var browser = new ManagedBrowserSession(new(site.Urls.Single()));
        await browser.PrepareAsync(draft, Approve(draft, ApprovalPurpose.ShareData), Resume);
        var store = site.Services.GetRequiredService<ReceiptStore>();
        Assert.Empty(store.Receipts);
        await Assert.ThrowsAsync<PolicyException>(() => browser.SubmitAsync(draft, null));
        Assert.Empty(store.Receipts);
        var evidence = await browser.SubmitAsync(draft, Approve(draft, ApprovalPurpose.Submit));
        Assert.NotNull(evidence);
        Assert.Equal(Resume.Hash, evidence.ResumeHash);
        Assert.Equal(draft.PayloadHash(), evidence.PayloadHash);
        Assert.Equal(draft.Id.ToString(), evidence.ApplicationKey);
        Assert.Single(store.Receipts);
        await Assert.ThrowsAsync<PolicyException>(() => browser.SubmitAsync(draft, Approve(draft, ApprovalPurpose.Submit)));
        Assert.Single(store.Receipts);
    }
}
