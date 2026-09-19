namespace JobAgent.Core.Evaluation;

public enum EvaluationSplit { Development, Test }
public enum EvaluationCaseKind { Answer, Job, Submission }
public enum EvaluationRunStatus { Completed, NotRun }

public enum EvaluationFixture
{
    AnswerKnownName,
    AnswerKnownEmail,
    AnswerKnownSalary,
    AnswerUnknown,
    AnswerUnverifiedName,
    JobThreeYearsRequired,
    JobFiveYearsRequired,
    JobUnknownWorkAuthorization,
    SubmissionVerified,
    SubmissionUnverified,
    SubmissionUnknown
}

public sealed record EvaluationExpectation
{
    public string? Status { get; init; }
    public string? Value { get; init; }
    public string? Decision { get; init; }
}

public sealed record EvaluationCase
{
    public string Id { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public EvaluationSplit Split { get; init; }
    public string GroupId { get; init; } = string.Empty;
    public string TemplateId { get; init; } = string.Empty;
    public EvaluationCaseKind Kind { get; init; }
    public EvaluationFixture Fixture { get; init; }
    public EvaluationExpectation Expected { get; init; } = new();
    public string RuleRationale { get; init; } = string.Empty;
}

public sealed record EvaluationDataset
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string DatasetId { get; init; } = string.Empty;
    public bool Synthetic { get; init; }
    public string License { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public List<EvaluationCase> Cases { get; init; } = [];
}

public sealed record LoadedEvaluationDataset(EvaluationDataset Dataset, string Sha256);

public sealed record EvaluationRunOptions(
    string CodeRevision,
    DateTimeOffset ScenarioAsOf,
    DateTimeOffset? RunAt = null,
    string ModeId = "fixture",
    string ModelId = "deterministic-rules-v1",
    string PromptId = "fixture-rules-v1");

public sealed record EvaluationMetric
{
    public string Id { get; init; } = string.Empty;
    public int Numerator { get; init; }
    public int Denominator { get; init; }
    public decimal? Rate => Denominator == 0 ? null : decimal.Round((decimal)Numerator / Denominator, 4);
}

public sealed record EvaluationCaseResult
{
    public string CaseId { get; init; } = string.Empty;
    public EvaluationCaseKind Kind { get; init; }
    public EvaluationSplit Split { get; init; }
    public string? ActualStatus { get; init; }
    public string? ActualDecision { get; init; }
    public bool ValueMatched { get; init; }
    public bool ExpectedOutcomeMatched { get; init; }
    public bool Abstained { get; init; }
    public string RuleRationale { get; init; } = string.Empty;
}

public sealed record EvaluationReport
{
    public string DatasetId { get; init; } = string.Empty;
    public string DatasetSha256 { get; init; } = string.Empty;
    public string CodeRevision { get; init; } = string.Empty;
    public DateTimeOffset ScenarioAsOf { get; init; }
    public DateTimeOffset RunAt { get; init; }
    public string ModeId { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string PromptId { get; init; } = string.Empty;
    public EvaluationRunStatus Status { get; init; }
    public EvaluationRunStatus RealModelStatus { get; init; }
    public int CaseCount { get; init; }
    public List<EvaluationMetric> Metrics { get; init; } = [];
    public List<EvaluationCaseResult> Results { get; init; } = [];
}
