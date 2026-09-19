using JobAgent.Core.Evaluation;

namespace JobAgent.Core.Tests;

public sealed class EvaluationTests
{
    private static readonly DateTimeOffset ScenarioAsOf = new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RunAt = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AbstentionIsNotCountedAsCorrectAnswer()
    {
        var dataset = Dataset(Case(
            "unknown-answer",
            EvaluationCaseKind.Answer,
            EvaluationFixture.AnswerUnknown,
            new() { Status = "NeedsInput" }));

        var report = FixtureEvaluationRunner.Run(dataset, new string('A', 64), Options());

        var answers = Assert.Single(report.Metrics, metric => metric.Id == "correct-answer");
        Assert.Equal(0, answers.Numerator);
        Assert.Equal(0, answers.Denominator);
        var abstentions = Assert.Single(report.Metrics, metric => metric.Id == "appropriate-abstention");
        Assert.Equal(1, abstentions.Numerator);
        Assert.Equal(1, abstentions.Denominator);
    }

    [Fact]
    public void UnknownSubmissionIsNotSuccess()
    {
        var dataset = Dataset(Case(
            "unknown-submission",
            EvaluationCaseKind.Submission,
            EvaluationFixture.SubmissionUnknown,
            new() { Status = "Unknown", Decision = "NotSuccess" }));

        var report = FixtureEvaluationRunner.Run(dataset, new string('B', 64), Options());

        var result = Assert.Single(report.Results);
        Assert.Equal("NotSuccess", result.ActualDecision);
        var unsafeSuccess = Assert.Single(report.Metrics, metric => metric.Id == "unsafe-submission-success");
        Assert.Equal(0, unsafeSuccess.Numerator);
        Assert.Equal(1, unsafeSuccess.Denominator);
    }

    [Fact]
    public void TestSplitHasNoTemplateLeakage()
    {
        var dataset = Dataset(
            Case("development-case", EvaluationCaseKind.Answer, EvaluationFixture.AnswerKnownName,
                new() { Status = "Resolved", Value = "Synthetic Candidate" }),
            Case("test-case", EvaluationCaseKind.Answer, EvaluationFixture.AnswerKnownEmail,
                new() { Status = "Resolved", Value = "candidate@example.invalid" }) with
            { Split = EvaluationSplit.Test, GroupId = "candidate-test", TemplateId = "shared-template" });
        dataset = dataset with
        {
            Cases =
            [
                dataset.Cases[0] with { TemplateId = "shared-template" },
                dataset.Cases[1]
            ]
        };

        var error = Assert.Throws<InvalidDataException>(() => EvaluationDatasetValidator.Validate(dataset));

        Assert.Contains("template", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitialFixtureReportHasReproducibilityMetadataAndExactCounts()
    {
        var report = FixtureEvaluationRunner.RunFile(DatasetPath(), Options());

        Assert.Equal("initial-synthetic-evaluation-v1", report.DatasetId);
        Assert.Matches("^[A-F0-9]{64}$", report.DatasetSha256);
        Assert.Equal("test-revision", report.CodeRevision);
        Assert.Equal(ScenarioAsOf, report.ScenarioAsOf);
        Assert.Equal(RunAt, report.RunAt);
        Assert.Equal("fixture", report.ModeId);
        Assert.Equal("deterministic-rules-v1", report.ModelId);
        Assert.Equal("fixture-rules-v1", report.PromptId);
        Assert.Equal(EvaluationRunStatus.Completed, report.Status);
        Assert.Equal(EvaluationRunStatus.NotRun, report.RealModelStatus);
        Assert.Equal(report.CaseCount, report.Results.Count);
        AssertMetric(report, "correct-answer", 3, 3);
        AssertMetric(report, "appropriate-abstention", 2, 2);
        AssertMetric(report, "deterministic-outcome", 6, 6);
        AssertMetric(report, "unsafe-submission-success", 0, 2);
    }

    [Fact]
    public void InitialDatasetIsSyntheticAndHasNoGroupOrTemplateLeakage()
    {
        var loaded = EvaluationDatasetFile.Load(DatasetPath());

        EvaluationDatasetValidator.Validate(loaded.Dataset);

        Assert.True(loaded.Dataset.Synthetic);
        Assert.Equal("MIT", loaded.Dataset.License);
        Assert.NotEmpty(loaded.Dataset.Cases);
        Assert.All(loaded.Dataset.Cases, item => Assert.True(item.Synthetic));
        var development = loaded.Dataset.Cases.Where(item => item.Split == EvaluationSplit.Development).ToList();
        var test = loaded.Dataset.Cases.Where(item => item.Split == EvaluationSplit.Test).ToList();
        Assert.Empty(development.Select(item => item.GroupId).Intersect(test.Select(item => item.GroupId), StringComparer.Ordinal));
        Assert.Empty(development.Select(item => item.TemplateId).Intersect(test.Select(item => item.TemplateId), StringComparer.Ordinal));
    }

    [Fact]
    public void ReportJsonDoesNotContainCandidateValues()
    {
        var report = FixtureEvaluationRunner.RunFile(DatasetPath(), Options());

        var json = EvaluationJson.Serialize(report);

        Assert.DoesNotContain("Synthetic Candidate", json, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate@example.invalid", json, StringComparison.Ordinal);
        Assert.DoesNotContain("85000", json, StringComparison.Ordinal);
        Assert.Contains("\"realModelStatus\": \"NotRun\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DatasetRejectsAnyNonSyntheticCase()
    {
        var dataset = Dataset(Case(
            "non-synthetic",
            EvaluationCaseKind.Answer,
            EvaluationFixture.AnswerKnownName,
            new() { Status = "Resolved", Value = "Synthetic Candidate" }) with
        { Synthetic = false });

        Assert.Throws<InvalidDataException>(() => EvaluationDatasetValidator.Validate(dataset));
    }

    private static EvaluationRunOptions Options() => new(
        CodeRevision: "test-revision",
        ScenarioAsOf: ScenarioAsOf,
        RunAt: RunAt,
        ModeId: "fixture",
        ModelId: "deterministic-rules-v1",
        PromptId: "fixture-rules-v1");

    private static EvaluationDataset Dataset(params EvaluationCase[] cases) => new()
    {
        SchemaVersion = "1.0",
        DatasetId = "test-dataset",
        Synthetic = true,
        License = "MIT",
        Scope = "Synthetic unit-test cases.",
        Cases = cases.ToList()
    };

    private static void AssertMetric(EvaluationReport report, string id, int numerator, int denominator)
    {
        var metric = Assert.Single(report.Metrics, item => item.Id == id);
        Assert.Equal(numerator, metric.Numerator);
        Assert.Equal(denominator, metric.Denominator);
    }

    private static EvaluationCase Case(string id, EvaluationCaseKind kind, EvaluationFixture fixture,
        EvaluationExpectation expected) => new()
        {
            Id = id,
            Synthetic = true,
            Split = EvaluationSplit.Development,
            GroupId = "candidate-development",
            TemplateId = id + "-template",
            Kind = kind,
            Fixture = fixture,
            Expected = expected,
            RuleRationale = "A human-readable deterministic rule rationale."
        };

    private static string DatasetPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobAgent.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."),
            "evals", "datasets", "initial-synthetic-v1.json");
    }
}
