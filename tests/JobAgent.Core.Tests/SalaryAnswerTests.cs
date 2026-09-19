using JobAgent.Core.Answers;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class SalaryAnswerTests
{
    [Fact]
    public void ExpectedSalary_DoesNotRevealPrivateMinimum()
    {
        var result = AnswerResolver.Resolve(
            new() { Key = "salary.expected.monthly.net.TRY", Label = "Expected monthly net salary", Language = "en" },
            TestProfiles.Synthetic(), JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.Resolved, result.Status);
        Assert.Equal("100000", result.Value);
        Assert.DoesNotContain("85000", result.Reason);
    }

    [Fact]
    public void MonthlyNet_IsNotAnnualGross()
    {
        var result = AnswerResolver.Resolve(
            new() { Key = "salary.expected.yearly.gross.TRY", Label = "Expected annual gross salary" },
            TestProfiles.Synthetic(), JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.NeedsInput, result.Status);
        Assert.Null(result.Value);
    }

    [Fact]
    public void CurrentSalary_IsNotExpectedSalary()
    {
        var result = AnswerResolver.Resolve(
            new() { Key = "salary.current.monthly.net.TRY", Label = "Current salary" },
            TestProfiles.Synthetic(), JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.NeedsInput, result.Status);
        Assert.Null(result.Value);
    }
}
