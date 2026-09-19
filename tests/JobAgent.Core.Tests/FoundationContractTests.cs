using System.Reflection;
using Xunit;

namespace JobAgent.Core.Tests;

public sealed class FoundationContractTests
{
    [Fact]
    public void CandidateProfile_PublicContractExists()
    {
        var core = Assembly.Load("JobAgent.Core");

        var profile = core.GetType("JobAgent.Core.Profiles.CandidateProfile");

        Assert.NotNull(profile);
        Assert.NotNull(profile.GetProperty("Version"));
        Assert.NotNull(profile.GetProperty("Facts"));
        Assert.NotNull(profile.GetProperty("Salary"));
    }
}
