using JobAgent.Core.Applications;
using JobAgent.FakeCareerSite;
using JobAgent.Infrastructure.Browser;
using Microsoft.Extensions.DependencyInjection;

namespace JobAgent.E2E.Tests;

public sealed class FormControlTests
{
    [Fact]
    public async Task ExtendedForm_SelectCheckedCheckboxAndRadio_SubmitsExactPreferences()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
        await site.StartAsync();
        var draft = WithPreferences(BrowserPolicyTests.Draft(site.Urls.Single()),
            workMode: "remote", travel: "true", contactMethod: "email");
        await using IBrowserSession browser = new ManagedBrowserSession(new(site.Urls.Single()));

        await browser.PrepareAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume);
        var evidence = await browser.SubmitAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.Submit));

        Assert.NotNull(evidence);
        var receipt = Assert.Single(site.Services.GetRequiredService<ReceiptStore>().Receipts);
        Assert.Equal("remote", receipt.WorkMode);
        Assert.True(receipt.Travel);
        Assert.Equal("email", receipt.ContactMethod);
        Assert.Null(receipt.ContactWindow);
        Assert.Equal(1, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Fact]
    public async Task ConditionalThirdStep_UncheckedCheckboxAndPhoneText_SubmitsExactPreferences()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
        await site.StartAsync();
        var draft = WithPreferences(BrowserPolicyTests.Draft(site.Urls.Single()),
            workMode: "hybrid", travel: "false", contactMethod: "phone", contactWindow: "Weekday afternoons");
        await using IBrowserSession browser = new ManagedBrowserSession(new(site.Urls.Single()));

        await browser.PrepareAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume);
        var evidence = await browser.SubmitAsync(draft, BrowserPolicyTests.Approve(draft, ApprovalPurpose.Submit));

        Assert.NotNull(evidence);
        var receipt = Assert.Single(site.Services.GetRequiredService<ReceiptStore>().Receipts);
        Assert.Equal("hybrid", receipt.WorkMode);
        Assert.False(receipt.Travel);
        Assert.Equal("phone", receipt.ContactMethod);
        Assert.Equal("Weekday afternoons", receipt.ContactWindow);
    }

    [Fact]
    public async Task MissingConditionalAnswer_IsNeedsInputBeforeSubmission()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
        await site.StartAsync();
        var draft = WithPreferences(BrowserPolicyTests.Draft(site.Urls.Single()),
            workMode: "remote", travel: "false", contactMethod: "phone");
        await using IBrowserSession browser = new ManagedBrowserSession(new(site.Urls.Single()));

        var error = await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));

        Assert.Equal("NeedsInput", error.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Fact]
    public async Task UnknownRequiredControl_IsNeedsInputBeforeAnyFormValueIsSent()
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(
            ExtendedControls: true, Mutation: SyntheticFormMutation.UnknownRequiredControl));
        await site.StartAsync();
        var draft = WithPreferences(BrowserPolicyTests.Draft(site.Urls.Single()),
            workMode: "remote", travel: "true", contactMethod: "email");
        await using IBrowserSession browser = new ManagedBrowserSession(new(site.Urls.Single()));

        var error = await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));

        Assert.Equal("NeedsInput", error.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Theory]
    [InlineData(SyntheticFormMutation.ChangedSelectOption)]
    [InlineData(SyntheticFormMutation.UnexpectedHiddenField)]
    [InlineData(SyntheticFormMutation.DisabledControl)]
    [InlineData(SyntheticFormMutation.DuplicateRadioOption)]
    [InlineData(SyntheticFormMutation.UnboundedConditionalText)]
    public async Task ChangedControlContract_IsRejectedBeforeSubmission(SyntheticFormMutation mutation)
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true, Mutation: mutation));
        await site.StartAsync();
        var draft = WithPreferences(BrowserPolicyTests.Draft(site.Urls.Single()),
            workMode: "hybrid", travel: "false", contactMethod: "phone", contactWindow: "Mornings");
        await using IBrowserSession browser = new ManagedBrowserSession(new(site.Urls.Single()));

        var error = await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));

        Assert.Equal("FormChanged", error.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().SubmissionPosts);
    }

    [Theory]
    [InlineData("preference.unknown", "value", "UnsupportedAnswer")]
    [InlineData("preference.work.mode", "office", "InvalidAnswer")]
    [InlineData("preference.travel", "yes", "InvalidAnswer")]
    public async Task UnknownOrInvalidDraftPreference_IsRejectedBeforeNavigation(
        string key, string value, string expectedCode)
    {
        await using var site = FakeCareerHost.Build("http://127.0.0.1:0", new(ExtendedControls: true));
        await site.StartAsync();
        var answers = new SortedDictionary<string, string>(BrowserPolicyTests.Draft(site.Urls.Single()).Answers,
            StringComparer.Ordinal)
        { [key] = value };
        if (key != "preference.unknown")
        {
            answers["preference.work.mode"] = key == "preference.work.mode" ? value : "remote";
            answers["preference.travel"] = key == "preference.travel" ? value : "false";
            answers["preference.contact.method"] = "email";
        }
        var draft = BrowserPolicyTests.Draft(site.Urls.Single()) with { Answers = answers };
        await using IBrowserSession browser = new ManagedBrowserSession(new(site.Urls.Single()));

        var error = await Assert.ThrowsAsync<PolicyException>(() => browser.PrepareAsync(draft,
            BrowserPolicyTests.Approve(draft, ApprovalPurpose.ShareData), BrowserPolicyTests.Resume));

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(0, site.Services.GetRequiredService<ReceiptStore>().Requests);
    }

    private static ApplicationDraft WithPreferences(ApplicationDraft draft, string workMode,
        string travel, string contactMethod, string? contactWindow = null)
    {
        var answers = new SortedDictionary<string, string>(draft.Answers, StringComparer.Ordinal)
        {
            ["preference.work.mode"] = workMode,
            ["preference.travel"] = travel,
            ["preference.contact.method"] = contactMethod
        };
        if (contactWindow is not null) answers["preference.contact.window"] = contactWindow;
        return draft with { Answers = answers };
    }
}
