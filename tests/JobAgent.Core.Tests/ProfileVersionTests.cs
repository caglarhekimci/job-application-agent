using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class ProfileVersionTests
{
    [Fact]
    public void CurrentVerifiedFact_IsUsable()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Facts = [new() { Id = "verified", VerificationStatus = VerificationStatus.Verified, ValidUntil = TestProfiles.Now.AddDays(1) }]
        };

        Assert.Equal("verified", Assert.Single(ProfilePolicy.UsableVerifiedFacts(profile, TestProfiles.Now)).Id);
    }

    [Fact]
    public void ProposedFact_IsNotVerifiedFact()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Facts = [new() { Id = "proposal", Kind = "skill", Value = "Rust", VerificationStatus = VerificationStatus.Proposed }]
        };

        var facts = ProfilePolicy.UsableVerifiedFacts(profile, TestProfiles.Now);

        Assert.Empty(facts);
    }

    [Fact]
    public void ExpiredVerifiedFact_IsNotUsable()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Facts = [new() { Id = "expired", VerificationStatus = VerificationStatus.Verified, ValidUntil = TestProfiles.Now.AddSeconds(-1) }]
        };

        Assert.Empty(ProfilePolicy.UsableVerifiedFacts(profile, TestProfiles.Now));
    }

    [Fact]
    public void ConfirmLocally_RecordsOnlySelectedFields()
    {
        var profile = JobAgent.Core.SyntheticData.Profile();

        var confirmed = ProfilePolicy.ConfirmLocally(profile, TestProfiles.Now, ProfileField.FullName, ProfileField.Email);

        Assert.True(ProfilePolicy.IsLocallyConfirmed(confirmed, ProfileField.FullName));
        Assert.True(ProfilePolicy.IsLocallyConfirmed(confirmed, ProfileField.Email));
        Assert.False(ProfilePolicy.IsLocallyConfirmed(confirmed, ProfileField.Salary));
    }
}
