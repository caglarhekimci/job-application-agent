using JobAgent.Core.Answers;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class ContactAndExperienceAnswerTests
{
    [Fact]
    public void ConfirmedEmail_IsResolved()
    {
        var result = AnswerResolver.Resolve(new() { Key = "contact.email", Label = "Email" }, TestProfiles.Synthetic(), JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal("candidate@example.invalid", result.Value);
    }

    [Fact]
    public void UnconfirmedName_RequiresReview()
    {
        var profile = TestProfiles.Synthetic() with { LocalConfirmations = [] };

        var result = AnswerResolver.Resolve(new() { Key = "contact.name", Label = "Name" }, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal(AnswerStatus.RequiresReview, result.Status);
    }

    [Fact]
    public void ProfessionalCSharpYears_UsesVerifiedProfessionalEvidence()
    {
        var result = AnswerResolver.Resolve(new() { Key = "experience.professional.csharp.years", Label = "Professional C# years" }, TestProfiles.Synthetic(), JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal("3", result.Value);
        Assert.Equal(["fact-csharp-professional"], result.EvidenceIds);
    }

    [Fact]
    public void FutureExperience_IsNotCountedBeforeItOccurs()
    {
        var profile = TestProfiles.Synthetic() with
        {
            Experience =
            [
                new()
                {
                    Start = new DateOnly(2025, 9, 18),
                    End = new DateOnly(2028, 9, 18),
                    Role = "Current role",
                    Kind = ExperienceKind.Professional,
                    Skills = ["C#"],
                    EvidenceIds = ["fact-csharp-professional"]
                }
            ]
        };

        var result = AnswerResolver.Resolve(new() { Key = "experience.professional.csharp.years", Label = "Professional C# years" }, profile, JobAgent.Core.SyntheticData.Job(), TestProfiles.Now);

        Assert.Equal("1", result.Value);
    }
}
