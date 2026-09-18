using JobAgent.Core.Applications;

namespace JobAgent.E2E.Tests;

public sealed class ApprovalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-18T00:00:00Z");
    private static ApplicationDraft Draft() => new()
    {
        ProfileId = Guid.NewGuid(), ProfileVersion = 1, JobKey = "sandbox:dotnet",
        Employer = "Synthetic Labs", RecipientOrigin = "http://127.0.0.1:5179",
        ResumeHash = "HASH-A", ResumeRef = "fixture", Synthetic = true,
        Answers = new() { ["salary"] = "100000" }
    };

    [Fact]
    public void CannotSubmitWithoutApproval() => Assert.Throws<PolicyException>(() =>
        ApprovalPolicy.Validate(Draft(), null, ApprovalPurpose.Submit, Now));

    [Theory]
    [InlineData("cv")]
    [InlineData("answers")]
    [InlineData("recipient")]
    [InlineData("profile")]
    [InlineData("job")]
    public void ChangedPackage_InvalidatesApproval(string field)
    {
        var draft = Draft();
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.Submit, "session", Now);
        var changed = field switch
        {
            "cv" => draft with { ResumeHash = "HASH-B" },
            "answers" => draft with { Answers = new() { ["salary"] = "85000" } },
            "recipient" => draft with { RecipientOrigin = "https://other.invalid" },
            "profile" => draft with { ProfileVersion = 2 },
            _ => draft with { JobKey = "sandbox:other" }
        };
        Assert.Throws<PolicyException>(() => ApprovalPolicy.Validate(changed, receipt, ApprovalPurpose.Submit, Now));
    }

    [Fact]
    public void ExpiredApproval_IsRejected()
    {
        var draft = Draft();
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.Submit, "session", Now);
        Assert.Throws<PolicyException>(() => ApprovalPolicy.Validate(draft, receipt, ApprovalPurpose.Submit, Now.AddMinutes(10)));
    }

    [Fact]
    public void SharingConsent_IsNotSubmissionApproval()
    {
        var draft = Draft();
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.ShareData, "session", Now);
        Assert.Throws<PolicyException>(() => ApprovalPolicy.Validate(draft, receipt, ApprovalPurpose.Submit, Now));
    }

    [Fact]
    public void Approval_IsSingleUse()
    {
        var draft = Draft();
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.Submit, "session", Now);
        var consumed = ApprovalPolicy.Consume(draft, receipt, ApprovalPurpose.Submit, Now);
        Assert.NotNull(consumed.UsedAt);
        Assert.Throws<PolicyException>(() => ApprovalPolicy.Consume(draft, consumed, ApprovalPurpose.Submit, Now));
    }

    [Fact]
    public void ValidApproval_AllowsOnlyItsBoundPackage()
    {
        var draft = Draft();
        var receipt = ApprovalPolicy.GrantFromUserInterface(draft, ApprovalPurpose.Submit, "session", Now);
        Assert.Null(Record.Exception(() => ApprovalPolicy.Validate(draft, receipt, ApprovalPurpose.Submit, Now)));
        Assert.Equal(draft.PayloadHash(), receipt.PayloadHash);
    }
}
