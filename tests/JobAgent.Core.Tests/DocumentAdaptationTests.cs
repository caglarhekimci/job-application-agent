using JobAgent.Core.Applications;
using JobAgent.Core.Documents;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Tests;

public sealed class DocumentAdaptationTests
{
    [Fact]
    public void Create_UsesOnlyExactCurrentVerifiedStatementsAndKeepsExperienceKindsSeparate()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var input = Input(now);

        var proposal = DocumentAdapter.Create(input, now);

        Assert.Contains("Built payment services in C#.", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.Contains("Created a personal weather application.", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Suggested unreviewed achievement", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Expired consulting engagement", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.Contains("Professional experience", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.Contains("Personal projects", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Internship experience", proposal.Resume.Content, StringComparison.Ordinal);
        Assert.All(proposal.Resume.Citations,
            citation => Assert.Contains(citation.ExactText, proposal.Resume.Content, StringComparison.Ordinal));
        Assert.Equal(ExperienceKind.Professional, proposal.Resume.Citations[0].ExperienceKind);
    }

    [Fact]
    public void Create_IsDeterministicAndBindsApplicationProfileResumeJobAndCurrentFacts()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var input = Input(now);

        var first = DocumentAdapter.Create(input, now);
        var second = DocumentAdapter.Create(input, now.AddMinutes(1));
        var changedResume = DocumentAdapter.Create(input with { ResumeHash = new string('b', 64) }, now);
        var afterExpiry = DocumentAdapter.Create(input, now.AddDays(2));

        Assert.Equal(first.BindingHash, second.BindingHash);
        Assert.Equal(first.BundleHash, second.BundleHash);
        Assert.NotEqual(first.BindingHash, changedResume.BindingHash);
        Assert.NotEqual(first.BindingHash, afterExpiry.BindingHash);
        Assert.DoesNotContain("Built payment services in C#.", afterExpiry.Resume.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsVerifiedEvidenceThatCannotBeProvenAgainstTheImportedSource()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var input = Input(now);
        var tampered = input with
        {
            Profile = input.Profile with
            {
                Facts = input.Profile.Facts.Select(fact => fact.Id == "professional"
                    ? fact with { Value = "Invented leadership claim." }
                    : fact).ToList()
            }
        };

        var error = Assert.Throws<DocumentAdaptationException>(() => DocumentAdapter.Create(tampered, now));

        Assert.Equal("AdaptationEvidenceInvalid", error.Code);
    }

    [Fact]
    public void Create_RejectsEvidenceLinkedToConflictingExperienceKinds()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var input = Input(now);
        var conflicting = input with
        {
            Profile = input.Profile with
            {
                Experience =
                [
                    .. input.Profile.Experience,
                    new ExperiencePeriod
                    {
                        Start = new DateOnly(2024, 1, 1),
                        Role = "Intern",
                        Kind = ExperienceKind.Internship,
                        Skills = ["C#"],
                        EvidenceIds = ["professional"]
                    }
                ]
            }
        };

        var error = Assert.Throws<DocumentAdaptationException>(() => DocumentAdapter.Create(conflicting, now));

        Assert.Equal("AdaptationEvidenceInvalid", error.Code);
    }

    [Fact]
    public void Create_RejectsFactKindThatContradictsTheReviewedExperienceKind()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var input = Input(now);
        var contradictory = input with
        {
            Profile = input.Profile with
            {
                Facts = input.Profile.Facts.Select(fact => fact.Id == "professional"
                    ? fact with { Kind = ExperienceKind.PersonalProject.ToString() }
                    : fact).ToList()
            }
        };

        var error = Assert.Throws<DocumentAdaptationException>(() => DocumentAdapter.Create(contradictory, now));

        Assert.Equal("AdaptationEvidenceInvalid", error.Code);
    }

    [Fact]
    public void Create_RefusesWhenNoCurrentVerifiedExactStatementExists()
    {
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var input = Input(now) with
        {
            Profile = Input(now).Profile with
            {
                Facts = Input(now).Profile.Facts.Select(fact => fact with
                {
                    VerificationStatus = VerificationStatus.Proposed
                }).ToList()
            }
        };

        var error = Assert.Throws<DocumentAdaptationException>(() => DocumentAdapter.Create(input, now));

        Assert.Equal("AdaptationEvidenceRequired", error.Code);
    }

    private static DocumentAdaptationInput Input(DateTimeOffset now)
    {
        var professional = new EvidenceFact
        {
            Id = "professional",
            Kind = "Professional",
            Value = "Built payment services in C#.",
            SourceDocumentId = "resume-document",
            SourceSpan = "line 2",
            VerificationStatus = VerificationStatus.Verified,
            ValidUntil = now.AddDays(1)
        };
        var project = new EvidenceFact
        {
            Id = "project",
            Kind = "PersonalProject",
            Value = "Created a personal weather application.",
            SourceDocumentId = "resume-document",
            SourceSpan = "line 1",
            VerificationStatus = VerificationStatus.Verified
        };
        var proposed = new EvidenceFact
        {
            Id = "proposed",
            Kind = "Professional",
            Value = "Suggested unreviewed achievement",
            SourceDocumentId = "resume-document",
            SourceSpan = "line 3",
            VerificationStatus = VerificationStatus.Proposed
        };
        var expired = new EvidenceFact
        {
            Id = "expired",
            Kind = "Professional",
            Value = "Expired consulting engagement",
            SourceDocumentId = "resume-document",
            SourceSpan = "line 4",
            VerificationStatus = VerificationStatus.Verified,
            ValidUntil = now.AddTicks(-1)
        };
        return new DocumentAdaptationInput
        {
            ApplicationRef = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            ApplicationPayloadHash = new string('a', 64),
            Profile = new CandidateProfile
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                Version = 4,
                VerifiedAt = now.AddDays(-1),
                Facts = [professional, project, proposed, expired],
                Experience =
                [
                    new ExperiencePeriod
                    {
                        Start = new DateOnly(2022, 1, 1), Role = "Developer",
                        Kind = ExperienceKind.Professional, Skills = ["C#"], EvidenceIds = [professional.Id]
                    },
                    new ExperiencePeriod
                    {
                        Start = new DateOnly(2021, 1, 1), Role = "Maker",
                        Kind = ExperienceKind.PersonalProject, Skills = ["TypeScript"], EvidenceIds = [project.Id]
                    }
                ]
            },
            ResumeRef = Guid.Parse("30000000-0000-0000-0000-000000000003"),
            ResumeHash = new string('a', 64),
            SourceDocumentId = "resume-document",
            OriginalText = string.Join('\n',
                "Created a personal weather application.",
                "Built payment services in C#.",
                "Suggested unreviewed achievement",
                "Expired consulting engagement"),
            SourceSegments =
            [
                new("line 1", "Created a personal weather application."),
                new("line 2", "Built payment services in C#."),
                new("line 3", "Suggested unreviewed achievement"),
                new("line 4", "Expired consulting engagement")
            ],
            JobRef = Guid.Parse("40000000-0000-0000-0000-000000000004"),
            Job = new JobPosting
            {
                Id = "40000000-0000-0000-0000-000000000004",
                Employer = "Example Employer",
                Title = ".NET Developer",
                TextHash = new string('c', 64),
                RequirementsReviewedAt = now.AddDays(-1),
                Requirements =
                [
                    new JobRequirement
                    {
                        Id = "csharp", RequirementText = "C# experience", Type = RequirementType.Skill,
                        Importance = RequirementImportance.Mandatory, Skill = "C#",
                        ReviewStatus = RequirementReviewStatus.Confirmed
                    }
                ]
            }
        };
    }
}
