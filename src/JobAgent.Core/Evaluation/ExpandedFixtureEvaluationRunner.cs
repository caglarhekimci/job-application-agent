using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobAgent.Core.Answers;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Evaluation;

public static class ExpandedFixtureEvaluationRunner
{
    public static ExpandedEvaluationReport RunFile(string datasetPath, EvaluationRunOptions options)
    {
        var loaded = ExpandedEvaluationDatasetFile.Load(datasetPath);
        return Run(loaded.Dataset, loaded.Sha256, options);
    }

    public static ExpandedEvaluationReport Run(ExpandedEvaluationDataset dataset, string datasetSha256,
        EvaluationRunOptions options)
    {
        ExpandedEvaluationDatasetValidator.Validate(dataset);
        ArgumentNullException.ThrowIfNull(options);
        Required(options.CodeRevision, nameof(options.CodeRevision));
        Required(options.ModeId, nameof(options.ModeId));
        Required(options.ModelId, nameof(options.ModelId));
        Required(options.PromptId, nameof(options.PromptId));
        if (datasetSha256.Length != 64 || datasetSha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Dataset SHA-256 must contain 64 hexadecimal characters.", nameof(datasetSha256));

        var profiles = dataset.Profiles.ToDictionary(item => item.Id,
            item => BuildProfile(item, options.ScenarioAsOf), StringComparer.Ordinal);
        var results = new List<ExpandedEvaluationCaseResult>(dataset.Questions.Count + dataset.Jobs.Count);
        foreach (var item in dataset.Questions)
        {
            var actual = ExecuteQuestion(item.Scenario, profiles[item.ProfileId], options.ScenarioAsOf);
            var valueMatched = string.Equals(actual.Value, item.Expected.Value, StringComparison.Ordinal);
            var matched = string.Equals(actual.Status, item.Expected.Status, StringComparison.Ordinal) && valueMatched;
            results.Add(new()
            {
                CaseId = item.Id,
                Kind = "Question",
                Split = item.Split,
                Scenario = item.Scenario.ToString(),
                ActualStatus = actual.Status,
                ValueMatched = valueMatched,
                ExpectedOutcomeMatched = matched,
                Abstained = actual.Status != nameof(AnswerStatus.Resolved),
                RuleRationale = item.RuleRationale
            });
        }
        foreach (var item in dataset.Jobs)
        {
            var actual = ExecuteJob(item.Scenario, profiles[item.ProfileId], options.ScenarioAsOf);
            var matched = string.Equals(actual, item.Expected.Status, StringComparison.Ordinal);
            results.Add(new()
            {
                CaseId = item.Id,
                Kind = "Job",
                Split = item.Split,
                Scenario = item.Scenario.ToString(),
                ActualStatus = actual,
                ValueMatched = true,
                ExpectedOutcomeMatched = matched,
                RuleRationale = item.RuleRationale
            });
        }

        var questionResults = results.Where(item => item.Kind == "Question").ToList();
        var jobResults = results.Where(item => item.Kind == "Job").ToList();
        var answerableIds = dataset.Questions.Where(item => item.Expected.Status == nameof(AnswerStatus.Resolved))
            .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var abstentionIds = dataset.Questions.Where(item => item.Expected.Status != nameof(AnswerStatus.Resolved))
            .Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var answerable = questionResults.Where(item => answerableIds.Contains(item.CaseId)).ToList();
        var abstentions = questionResults.Where(item => abstentionIds.Contains(item.CaseId)).ToList();

        return new()
        {
            DatasetId = dataset.DatasetId,
            DatasetSha256 = datasetSha256.ToUpperInvariant(),
            CodeRevision = options.CodeRevision,
            ScenarioAsOf = options.ScenarioAsOf,
            RunAt = options.RunAt ?? DateTimeOffset.UtcNow,
            ModeId = options.ModeId,
            ModelId = options.ModelId,
            PromptId = options.PromptId,
            ProfileGroupCount = dataset.Profiles.Count,
            QuestionCaseCount = dataset.Questions.Count,
            JobCaseCount = dataset.Jobs.Count,
            B0Status = EvaluationRunStatus.Completed,
            B1Status = EvaluationRunStatus.NotRun,
            B2Status = EvaluationRunStatus.NotRun,
            Results = results,
            Metrics =
            [
                Metric("b0-question-outcome", questionResults.Count(item => item.ExpectedOutcomeMatched), questionResults.Count),
                Metric("b0-answerable-correct", answerable.Count(item => item.ExpectedOutcomeMatched && !item.Abstained), answerable.Count),
                Metric("b0-appropriate-abstention", abstentions.Count(item => item.ExpectedOutcomeMatched && item.Abstained), abstentions.Count),
                Metric("b0-job-outcome", jobResults.Count(item => item.ExpectedOutcomeMatched), jobResults.Count)
            ]
        };
    }

    private static CandidateProfile BuildProfile(ExpandedProfileFixture fixture, DateTimeOffset now)
    {
        var config = Configuration(fixture.Archetype);
        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);
        var factId = fixture.Id + "-fact-csharp";
        var proposedFactId = fixture.Id + "-fact-proposed";
        var confirmations = new List<LocalConfirmation>();
        if (config.ContactsConfirmed)
        {
            confirmations.Add(new() { Field = ProfileField.FullName, ConfirmedAt = now.AddDays(-1) });
            confirmations.Add(new() { Field = ProfileField.Email, ConfirmedAt = now.AddDays(-1) });
        }
        if (config.SalaryConfirmed)
            confirmations.Add(new() { Field = ProfileField.Salary, ConfirmedAt = now.AddDays(-1) });

        return new()
        {
            Id = DeterministicGuid(fixture.Id),
            Version = 1,
            Synthetic = true,
            FullName = "Synthetic " + fixture.Archetype,
            Email = fixture.Archetype.ToString().ToLowerInvariant() + "@example.invalid",
            Locale = config.Locale,
            VerifiedAt = now.AddDays(-1),
            Facts =
            [
                new()
                {
                    Id = factId,
                    Kind = "professional-skill",
                    Value = "C#",
                    SourceDocumentId = "synthetic-" + fixture.Id,
                    SourceSpan = "line 1",
                    VerificationStatus = VerificationStatus.Verified,
                    ValidFrom = now.AddYears(-6),
                    ValidUntil = config.EvidenceExpired ? now.AddTicks(-1) : null
                },
                new()
                {
                    Id = proposedFactId,
                    Kind = "unverified-preference",
                    Value = "Unverified",
                    SourceDocumentId = "synthetic-" + fixture.Id,
                    SourceSpan = "line 2",
                    VerificationStatus = VerificationStatus.Proposed,
                    ValidFrom = now.AddDays(-1)
                }
            ],
            Experience =
            [
                new()
                {
                    Start = currentDate.AddYears(-config.ExperienceYears),
                    End = currentDate,
                    Role = "Synthetic role",
                    Kind = config.ExperienceKind,
                    Skills = ["C#"],
                    EvidenceIds = [factId]
                }
            ],
            Salary = new()
            {
                Target = new()
                {
                    Amount = config.SalaryConfirmed ? 100000m : 0m,
                    Currency = "TRY",
                    Period = config.AnnualGrossSalary ? SalaryPeriod.Year : SalaryPeriod.Month,
                    TaxBasis = config.AnnualGrossSalary ? TaxBasis.Gross : TaxBasis.Net
                },
                PrivateMinimum = new()
                {
                    Amount = config.SalaryConfirmed ? 85000m : 0m,
                    Currency = "TRY",
                    Period = SalaryPeriod.Month,
                    TaxBasis = TaxBasis.Net
                },
                ConfirmedAt = config.SalaryConfirmed ? now.AddDays(-1) : null
            },
            LocalConfirmations = confirmations,
            Answers = CommonAnswers(factId, proposedFactId, now)
        };
    }

    private static List<AnswerMemory> CommonAnswers(string factId, string proposedFactId, DateTimeOffset now) =>
    [
        new() { SemanticKey = "availability", Answer = "Two weeks", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-2) },
        new() { SemanticKey = "availability", Answer = "One week", Scope = AnswerScopeType.Company,
            ScopeId = "Synthetic Employer", Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "motivation", Answer = "Application-specific statement",
            Scope = AnswerScopeType.Application, ScopeId = "synthetic-job-1", Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "relocation", Answer = "Evet", Scope = AnswerScopeType.Default,
            Language = "tr", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "relocation", Answer = "Yes", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "expired.preference", Answer = "Expired", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-2), ExpiresAt = now },
        new() { SemanticKey = "evidence.preference", Answer = "Grounded", Scope = AnswerScopeType.Default,
            Language = "en", EvidenceIds = [factId], UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "long.answer", Answer = "abcdefghijkl", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "notice.period", Answer = "30 days", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "notice.period", Answer = "30 gün", Scope = AnswerScopeType.Default,
            Language = "tr", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "travel", Answer = "No", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-2) },
        new() { SemanticKey = "travel", Answer = "Yes", Scope = AnswerScopeType.Company,
            ScopeId = "Synthetic Employer", Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "cover", Answer = "Reviewed cover statement", Scope = AnswerScopeType.Application,
            ScopeId = "synthetic-job-1", Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "preferred.location", Answer = "Remote", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "preferred.location", Answer = "Uzaktan", Scope = AnswerScopeType.Default,
            Language = "tr", UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "future.expiry", Answer = "Current", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-1), ExpiresAt = now.AddDays(1) },
        new() { SemanticKey = "missing.evidence", Answer = "Unsupported", Scope = AnswerScopeType.Default,
            Language = "en", EvidenceIds = ["missing-fact"], UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "unverified.evidence", Answer = "Unverified", Scope = AnswerScopeType.Default,
            Language = "en", EvidenceIds = [proposedFactId], UpdatedAt = now.AddDays(-1) },
        new() { SemanticKey = "exact.length", Answer = "Yes", Scope = AnswerScopeType.Default,
            Language = "en", UpdatedAt = now.AddDays(-1) }
    ];

    private static ActualAnswer ExecuteQuestion(ExpandedQuestionScenario scenario,
        CandidateProfile profile, DateTimeOffset now)
    {
        var (question, job) = scenario switch
        {
            ExpandedQuestionScenario.KnownName => (Question("contact.name", "en"), CurrentJob()),
            ExpandedQuestionScenario.KnownEmail => (Question("contact.email", "en"), CurrentJob()),
            ExpandedQuestionScenario.ExpectedMonthlyNetSalary =>
                (Question("salary.expected.monthly.net.TRY", "en"), CurrentJob()),
            ExpandedQuestionScenario.ExpectedAnnualGrossSalary =>
                (Question("salary.expected.yearly.gross.TRY", "en"), CurrentJob()),
            ExpandedQuestionScenario.CurrentSalary =>
                (Question("salary.current.monthly.net.TRY", "en"), CurrentJob()),
            ExpandedQuestionScenario.ProfessionalCsharpYears =>
                (Question("experience.professional.csharp.years", "en"), CurrentJob()),
            ExpandedQuestionScenario.UnknownField => (Question("unknown.claim", "en"), CurrentJob()),
            ExpandedQuestionScenario.SensitiveName =>
                (Question("contact.name", "en") with { Sensitive = true }, CurrentJob()),
            ExpandedQuestionScenario.AttestedEmail =>
                (Question("contact.email", "en") with { RequiresCandidateAttestation = true }, CurrentJob()),
            ExpandedQuestionScenario.CompanyAvailabilityCurrent =>
                (Question("availability", "en"), CurrentJob()),
            ExpandedQuestionScenario.CompanyAvailabilityOther =>
                (Question("availability", "en"), OtherJob()),
            ExpandedQuestionScenario.ApplicationMotivationCurrent =>
                (Question("motivation", "en"), CurrentJob()),
            ExpandedQuestionScenario.ApplicationMotivationOther =>
                (Question("motivation", "en"), OtherJob()),
            ExpandedQuestionScenario.RelocationTurkish => (Question("relocation", "tr"), CurrentJob()),
            ExpandedQuestionScenario.RelocationEnglish => (Question("relocation", "en"), CurrentJob()),
            ExpandedQuestionScenario.ExpiredPreference =>
                (Question("expired.preference", "en"), CurrentJob()),
            ExpandedQuestionScenario.EvidencePreference =>
                (Question("evidence.preference", "en"), CurrentJob()),
            ExpandedQuestionScenario.OverLengthAnswer =>
                (Question("long.answer", "en") with { MaxLength = 5 }, CurrentJob()),
            ExpandedQuestionScenario.UnsupportedLanguage =>
                (Question("relocation", "de"), CurrentJob()),
            ExpandedQuestionScenario.PortfolioUnknown => (Question("portfolio", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestNameTightLimit =>
                (Question("contact.name", "en") with { MaxLength = 10 }, CurrentJob()),
            ExpandedQuestionScenario.TestEmailTightLimit =>
                (Question("contact.email", "en") with { MaxLength = 12 }, CurrentJob()),
            ExpandedQuestionScenario.TestSalaryTightLimit =>
                (Question("salary.expected.monthly.net.TRY", "en") with { MaxLength = 5 }, CurrentJob()),
            ExpandedQuestionScenario.TestExperienceTightLimit =>
                (Question("experience.professional.csharp.years", "en") with { MaxLength = 0 }, CurrentJob()),
            ExpandedQuestionScenario.TestCertificationUnknown =>
                (Question("certification", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestSensitiveHealth =>
                (Question("health.status", "en") with { Sensitive = true }, CurrentJob()),
            ExpandedQuestionScenario.TestLegalAttestation =>
                (Question("legal.attestation", "en") with { RequiresCandidateAttestation = true }, CurrentJob()),
            ExpandedQuestionScenario.TestNoticeEnglish =>
                (Question("notice.period", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestNoticeTurkish =>
                (Question("notice.period", "tr"), CurrentJob()),
            ExpandedQuestionScenario.TestTravelCurrentCompany =>
                (Question("travel", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestTravelOtherCompany =>
                (Question("travel", "en"), OtherJob()),
            ExpandedQuestionScenario.TestCoverCurrentApplication =>
                (Question("cover", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestCoverOtherApplication =>
                (Question("cover", "en"), OtherJob()),
            ExpandedQuestionScenario.TestLocationEnglish =>
                (Question("preferred.location", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestLocationTurkish =>
                (Question("preferred.location", "tr"), CurrentJob()),
            ExpandedQuestionScenario.TestFutureExpiry =>
                (Question("future.expiry", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestMissingEvidence =>
                (Question("missing.evidence", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestUnverifiedEvidence =>
                (Question("unverified.evidence", "en"), CurrentJob()),
            ExpandedQuestionScenario.TestExactLength =>
                (Question("exact.length", "en") with { MaxLength = 3 }, CurrentJob()),
            ExpandedQuestionScenario.TestOverLength =>
                (Question("exact.length", "en") with { MaxLength = 2 }, CurrentJob()),
            _ => throw new InvalidDataException($"Unsupported expanded question scenario: {scenario}.")
        };
        var answer = AnswerResolver.Resolve(question, profile, job, now);
        return new(answer.Status.ToString(), answer.Value);
    }

    private static string ExecuteJob(ExpandedJobScenario scenario, CandidateProfile profile, DateTimeOffset now)
    {
        var requirement = scenario switch
        {
            ExpandedJobScenario.MandatoryCsharpTwoYears => ExperienceRequirement(2m, RequirementImportance.Mandatory),
            ExpandedJobScenario.PreferredCsharpFourYears => ExperienceRequirement(4m, RequirementImportance.Preferred),
            ExpandedJobScenario.WorkAuthorizationMandatory => new JobRequirement
            {
                Id = "authorization",
                RequirementText = "Work authorization required",
                Type = RequirementType.WorkAuthorization,
                Importance = RequirementImportance.Mandatory
            },
            ExpandedJobScenario.LanguageMandatory => new JobRequirement
            {
                Id = "language",
                RequirementText = "English required",
                Type = RequirementType.Language,
                Importance = RequirementImportance.Mandatory,
                ExpectedValue = "English"
            },
            ExpandedJobScenario.ClosedLocation => new JobRequirement
            {
                Id = "location",
                RequirementText = "Istanbul location",
                Type = RequirementType.Location,
                Importance = RequirementImportance.Mandatory,
                ExpectedValue = "Istanbul"
            },
            ExpandedJobScenario.TestMandatoryCsharpFourYears =>
                ExperienceRequirement(4m, RequirementImportance.Mandatory),
            ExpandedJobScenario.TestPreferredCsharpTwoYears =>
                ExperienceRequirement(2m, RequirementImportance.Preferred),
            ExpandedJobScenario.TestSkillMandatory => new JobRequirement
            {
                Id = "skill",
                RequirementText = "Distributed systems required",
                Type = RequirementType.Skill,
                Importance = RequirementImportance.Mandatory,
                ExpectedValue = "Distributed systems"
            },
            ExpandedJobScenario.TestContractMandatory => new JobRequirement
            {
                Id = "contract",
                RequirementText = "Permanent contract required",
                Type = RequirementType.Contract,
                Importance = RequirementImportance.Mandatory,
                ExpectedValue = "Permanent"
            },
            ExpandedJobScenario.TestClosedLanguage => new JobRequirement
            {
                Id = "closed-language",
                RequirementText = "Turkish required",
                Type = RequirementType.Language,
                Importance = RequirementImportance.Mandatory,
                ExpectedValue = "Turkish"
            },
            _ => throw new InvalidDataException($"Unsupported expanded job scenario: {scenario}.")
        };
        var job = CurrentJob() with
        {
            Availability = scenario is ExpandedJobScenario.ClosedLocation or ExpandedJobScenario.TestClosedLanguage
                ? JobAvailability.Closed : JobAvailability.Open,
            ClosedAt = scenario is ExpandedJobScenario.ClosedLocation or ExpandedJobScenario.TestClosedLanguage
                ? now.AddDays(-1) : null,
            Requirements = [requirement]
        };
        return JobEvaluator.Evaluate(job, profile, now).Status.ToString();
    }

    private static JobRequirement ExperienceRequirement(decimal years, RequirementImportance importance) => new()
    {
        Id = $"csharp-{years:0}",
        RequirementText = $"{years:0} years professional C# required",
        Type = RequirementType.ProfessionalExperienceYears,
        Importance = importance,
        Skill = "C#",
        MinimumYears = years
    };

    private static FormQuestion Question(string key, string language) => new()
    { Key = key, Label = key, Language = language };

    private static JobPosting CurrentJob() => SyntheticData.Job();

    private static JobPosting OtherJob() => SyntheticData.Job() with
    { Id = "other-job", Employer = "Other Employer" };

    private static ProfileConfiguration Configuration(ExpandedProfileArchetype archetype) => archetype switch
    {
        ExpandedProfileArchetype.DevExperiencedConfirmed => new(3, ExperienceKind.Professional, true, true, false, false, "en"),
        ExpandedProfileArchetype.DevJuniorConfirmed => new(1, ExperienceKind.Professional, true, true, false, false, "en"),
        ExpandedProfileArchetype.DevPersonalProjectOnly => new(5, ExperienceKind.PersonalProject, true, true, false, false, "en"),
        ExpandedProfileArchetype.DevUnverifiedContacts => new(3, ExperienceKind.Professional, false, true, false, false, "en"),
        ExpandedProfileArchetype.DevAnnualGrossSalary => new(3, ExperienceKind.Professional, true, true, true, false, "en"),
        ExpandedProfileArchetype.DevTurkishTwoYears => new(2, ExperienceKind.Professional, true, true, false, false, "tr"),
        ExpandedProfileArchetype.TestExperiencedFourYears => new(4, ExperienceKind.Professional, true, true, false, false, "en"),
        ExpandedProfileArchetype.TestInternshipOnly => new(3, ExperienceKind.Internship, true, true, false, false, "en"),
        ExpandedProfileArchetype.TestExpiredEvidence => new(3, ExperienceKind.Professional, true, true, false, true, "en"),
        ExpandedProfileArchetype.TestNoSalary => new(2, ExperienceKind.Professional, true, false, false, false, "en"),
        ExpandedProfileArchetype.TestTurkishFiveYears => new(5, ExperienceKind.Professional, true, true, false, false, "tr"),
        ExpandedProfileArchetype.TestScopedAnswers => new(3, ExperienceKind.Professional, true, true, false, false, "en"),
        _ => throw new InvalidDataException($"Unsupported expanded profile archetype: {archetype}.")
    };

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static EvaluationMetric Metric(string id, int numerator, int denominator) => new()
    { Id = id, Numerator = numerator, Denominator = denominator };

    private static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
    }

    private sealed record ActualAnswer(string Status, string? Value);
    private sealed record ProfileConfiguration(int ExperienceYears, ExperienceKind ExperienceKind,
        bool ContactsConfirmed, bool SalaryConfirmed, bool AnnualGrossSalary, bool EvidenceExpired, string Locale);
}

public static class ExpandedEvaluationJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(ExpandedEvaluationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, Options);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
