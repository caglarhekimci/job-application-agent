using System.Text.Json.Serialization;

namespace JobAgent.Core.Evaluation;

public enum ExpandedProfileArchetype
{
    DevExperiencedConfirmed,
    DevJuniorConfirmed,
    DevPersonalProjectOnly,
    DevUnverifiedContacts,
    DevAnnualGrossSalary,
    DevTurkishTwoYears,
    TestExperiencedFourYears,
    TestInternshipOnly,
    TestExpiredEvidence,
    TestNoSalary,
    TestTurkishFiveYears,
    TestScopedAnswers
}

public enum ExpandedQuestionScenario
{
    KnownName,
    KnownEmail,
    ExpectedMonthlyNetSalary,
    ExpectedAnnualGrossSalary,
    CurrentSalary,
    ProfessionalCsharpYears,
    UnknownField,
    SensitiveName,
    AttestedEmail,
    CompanyAvailabilityCurrent,
    CompanyAvailabilityOther,
    ApplicationMotivationCurrent,
    ApplicationMotivationOther,
    RelocationTurkish,
    RelocationEnglish,
    ExpiredPreference,
    EvidencePreference,
    OverLengthAnswer,
    UnsupportedLanguage,
    PortfolioUnknown,
    TestNameTightLimit,
    TestEmailTightLimit,
    TestSalaryTightLimit,
    TestExperienceTightLimit,
    TestCertificationUnknown,
    TestSensitiveHealth,
    TestLegalAttestation,
    TestNoticeEnglish,
    TestNoticeTurkish,
    TestTravelCurrentCompany,
    TestTravelOtherCompany,
    TestCoverCurrentApplication,
    TestCoverOtherApplication,
    TestLocationEnglish,
    TestLocationTurkish,
    TestFutureExpiry,
    TestMissingEvidence,
    TestUnverifiedEvidence,
    TestExactLength,
    TestOverLength
}

public enum ExpandedJobScenario
{
    MandatoryCsharpTwoYears,
    PreferredCsharpFourYears,
    WorkAuthorizationMandatory,
    LanguageMandatory,
    ClosedLocation,
    TestMandatoryCsharpFourYears,
    TestPreferredCsharpTwoYears,
    TestSkillMandatory,
    TestContractMandatory,
    TestClosedLanguage
}

public sealed record ExpandedProfileFixture
{
    public string Id { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public EvaluationSplit Split { get; init; }
    public string GroupId { get; init; } = string.Empty;
    public string FamilyId { get; init; } = string.Empty;
    public ExpandedProfileArchetype Archetype { get; init; }
}

public sealed record ExpandedQuestionFamily
{
    public bool Synthetic { get; init; }
    public EvaluationSplit Split { get; init; }
    public ExpandedQuestionScenario Scenario { get; init; }
    public string TemplateFamilyId { get; init; } = string.Empty;
    public string RuleRationale { get; init; } = string.Empty;
    public Dictionary<ExpandedProfileArchetype, EvaluationExpectation> ExpectedByArchetype { get; init; } = [];
}

public sealed record ExpandedJobFamily
{
    public bool Synthetic { get; init; }
    public EvaluationSplit Split { get; init; }
    public ExpandedJobScenario Scenario { get; init; }
    public string TemplateFamilyId { get; init; } = string.Empty;
    public string RuleRationale { get; init; } = string.Empty;
    public Dictionary<ExpandedProfileArchetype, EvaluationExpectation> ExpectedByArchetype { get; init; } = [];
}

public sealed record ExpandedQuestionCase
{
    public string Id { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public EvaluationSplit Split { get; init; }
    public string ProfileId { get; init; } = string.Empty;
    public string TemplateFamilyId { get; init; } = string.Empty;
    public ExpandedQuestionScenario Scenario { get; init; }
    public EvaluationExpectation Expected { get; init; } = new();
    public string RuleRationale { get; init; } = string.Empty;
}

public sealed record ExpandedJobCase
{
    public string Id { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public EvaluationSplit Split { get; init; }
    public string ProfileId { get; init; } = string.Empty;
    public string TemplateFamilyId { get; init; } = string.Empty;
    public ExpandedJobScenario Scenario { get; init; }
    public EvaluationExpectation Expected { get; init; } = new();
    public string RuleRationale { get; init; } = string.Empty;
}

public sealed record ExpandedEvaluationDataset
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string DatasetId { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public string License { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public List<ExpandedProfileFixture> Profiles { get; init; } = [];
    public List<ExpandedQuestionFamily> QuestionFamilies { get; init; } = [];
    public List<ExpandedJobFamily> JobFamilies { get; init; } = [];
    [JsonIgnore] public List<ExpandedQuestionCase> Questions { get; init; } = [];
    [JsonIgnore] public List<ExpandedJobCase> Jobs { get; init; } = [];
}

public sealed record LoadedExpandedEvaluationDataset(ExpandedEvaluationDataset Dataset, string Sha256);

public sealed record ExpandedEvaluationCaseResult
{
    public string CaseId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public EvaluationSplit Split { get; init; }
    public string Scenario { get; init; } = string.Empty;
    public string ActualStatus { get; init; } = string.Empty;
    public bool ValueMatched { get; init; }
    public bool ExpectedOutcomeMatched { get; init; }
    public bool Abstained { get; init; }
    public string RuleRationale { get; init; } = string.Empty;
}

public sealed record ExpandedEvaluationReport
{
    public string DatasetId { get; init; } = string.Empty;
    public string DatasetSha256 { get; init; } = string.Empty;
    public string CodeRevision { get; init; } = string.Empty;
    public DateTimeOffset ScenarioAsOf { get; init; }
    public DateTimeOffset RunAt { get; init; }
    public string ModeId { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string PromptId { get; init; } = string.Empty;
    public int ProfileGroupCount { get; init; }
    public int QuestionCaseCount { get; init; }
    public int JobCaseCount { get; init; }
    public EvaluationRunStatus B0Status { get; init; }
    public EvaluationRunStatus B1Status { get; init; }
    public EvaluationRunStatus B2Status { get; init; }
    public List<EvaluationMetric> Metrics { get; init; } = [];
    public List<ExpandedEvaluationCaseResult> Results { get; init; } = [];
}
