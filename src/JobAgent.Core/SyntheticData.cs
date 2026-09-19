using JobAgent.Core.Jobs;
using JobAgent.Core.Permissions;
using JobAgent.Core.Profiles;

namespace JobAgent.Core;

public static class SyntheticData
{
    private static readonly DateTimeOffset Snapshot = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static CandidateProfile Profile() => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Version = 1,
        Synthetic = true,
        FullName = "Synthetic Candidate",
        Email = "candidate@example.invalid",
        Locale = "en",
        VerifiedAt = Snapshot,
        Facts =
        [
            new()
            {
                Id = "fact-csharp-professional",
                Kind = "professional-skill",
                Value = "C#",
                SourceDocumentId = "synthetic-resume",
                SourceSpan = "line 4",
                VerificationStatus = VerificationStatus.Verified,
                ValidFrom = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
            }
        ],
        Experience =
        [
            new()
            {
                Start = new DateOnly(2023, 1, 1),
                End = new DateOnly(2026, 1, 1),
                Role = "Software Engineer",
                Kind = ExperienceKind.Professional,
                Skills = ["C#"],
                EvidenceIds = ["fact-csharp-professional"]
            }
        ],
        Salary = new()
        {
            Target = new() { Amount = 100000m, Currency = "TRY", Period = SalaryPeriod.Month, TaxBasis = TaxBasis.Net },
            PrivateMinimum = new() { Amount = 85000m, Currency = "TRY", Period = SalaryPeriod.Month, TaxBasis = TaxBasis.Net },
            Negotiable = true,
            DisclosePrivateMinimum = false,
            ConfirmedAt = Snapshot
        }
    };

    public static JobPosting Job() => new()
    {
        Id = "synthetic-job-1",
        Employer = "Synthetic Employer",
        Title = "C# Engineer",
        Text = "Synthetic posting for local tests only.",
        SourceUrl = "https://example.invalid/jobs/synthetic-job-1",
        Synthetic = true,
        SourcePermission = SourcePermission.ForUserProvidedText(Snapshot)
    };
}
