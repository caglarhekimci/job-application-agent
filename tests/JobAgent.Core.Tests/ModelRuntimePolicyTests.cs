using JobAgent.Core.Applications;
using JobAgent.Core.Models;
using Xunit;

namespace JobAgent.Core.Tests;

public sealed class ModelRuntimePolicyTests
{
    [Fact]
    public void ZeroPaidDefaultAllowsBoundedHostMediatedProposals()
    {
        var policy = ModelRuntimePolicy.ZeroPaidDefault;

        Assert.Equal(ModelProviderMode.HostMediated, policy.Mode);
        Assert.False(policy.PaidApiEnabled);
        Assert.Equal(4, policy.MaxAnswerProposalOperationsPerApplication);
        ModelRuntimePolicyRules.RequireHostProposal(policy);
        ModelRuntimePolicyRules.RequireAnswerProposal(policy, 3);
    }

    [Fact]
    public void ExactApplicationOperationLimitFailsClosed()
    {
        var policy = ModelRuntimePolicy.ZeroPaidDefault with
        {
            MaxAnswerProposalOperationsPerApplication = 2
        };

        var error = Assert.Throws<PolicyException>(() =>
            ModelRuntimePolicyRules.RequireAnswerProposal(policy, 2));

        Assert.Equal("BudgetExceeded", error.Code);
    }

    [Theory]
    [InlineData(ModelProviderMode.Api, "PaidApiDisabled")]
    [InlineData(ModelProviderMode.Fixture, "ProviderModeDisabled")]
    [InlineData((ModelProviderMode)999, "ProviderPolicyInvalid")]
    public void DisabledOrUnknownModesFailClosed(ModelProviderMode mode, string code)
    {
        var error = Assert.Throws<PolicyException>(() =>
            ModelRuntimePolicyRules.RequireHostProposal(ModelRuntimePolicy.ZeroPaidDefault with { Mode = mode }));

        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void NullOrOutOfRangeCapsFailClosed()
    {
        Assert.Equal("ProviderPolicyInvalid", Assert.Throws<PolicyException>(() =>
            ModelRuntimePolicyRules.RequireHostProposal(null)).Code);
        Assert.Equal("ProviderPolicyInvalid", Assert.Throws<PolicyException>(() =>
            ModelRuntimePolicyRules.RequireHostProposal(ModelRuntimePolicy.ZeroPaidDefault with
            {
                MaxAnswerProposalOperationsPerApplication = 0
            })).Code);
        Assert.Equal("ProviderPolicyInvalid", Assert.Throws<PolicyException>(() =>
            ModelRuntimePolicyRules.RequireHostProposal(ModelRuntimePolicy.ZeroPaidDefault with
            {
                MaxAnswerProposalOperationsPerApplication = ModelRuntimePolicy.HardMaximumOperations + 1
            })).Code);
    }
}
