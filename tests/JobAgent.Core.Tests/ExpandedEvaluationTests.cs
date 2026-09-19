using JobAgent.Core.Evaluation;

namespace JobAgent.Core.Tests;

public sealed class ExpandedEvaluationTests
{
    private static readonly DateTimeOffset ScenarioAsOf = new(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RunAt = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExpandedDatasetMeetsTargetsWithoutScenarioPadding()
    {
        var loaded = ExpandedEvaluationDatasetFile.Load(DatasetPath());

        Assert.True(loaded.Dataset.Synthetic);
        Assert.Equal("MIT", loaded.Dataset.License);
        Assert.Equal(12, loaded.Dataset.Profiles.Count);
        Assert.Equal(12, loaded.Dataset.Profiles.Select(item => item.Archetype).Distinct().Count());
        Assert.Equal(20, loaded.Dataset.QuestionFamilies.Count(item => item.Split == EvaluationSplit.Development));
        Assert.Equal(20, loaded.Dataset.QuestionFamilies.Count(item => item.Split == EvaluationSplit.Test));
        Assert.Equal(5, loaded.Dataset.JobFamilies.Count(item => item.Split == EvaluationSplit.Development));
        Assert.Equal(5, loaded.Dataset.JobFamilies.Count(item => item.Split == EvaluationSplit.Test));
        Assert.Equal(60, loaded.Dataset.Jobs.Count);
        Assert.Equal(240, loaded.Dataset.Questions.Count);
        Assert.All(loaded.Dataset.Profiles, profile =>
        {
            Assert.Equal(5, loaded.Dataset.Jobs.Where(item => item.ProfileId == profile.Id)
                .Select(item => item.Scenario).Distinct().Count());
            Assert.Equal(20, loaded.Dataset.Questions.Where(item => item.ProfileId == profile.Id)
                .Select(item => item.Scenario).Distinct().Count());
        });
        Assert.All(loaded.Dataset.Jobs, item => Assert.False(string.IsNullOrWhiteSpace(item.RuleRationale)));
        Assert.All(loaded.Dataset.Questions, item => Assert.False(string.IsNullOrWhiteSpace(item.RuleRationale)));
    }

    [Fact]
    public void ExpandedDatasetHasWholeFamilySplitIsolation()
    {
        var dataset = ExpandedEvaluationDatasetFile.Load(DatasetPath()).Dataset;
        var developmentProfiles = dataset.Profiles.Where(item => item.Split == EvaluationSplit.Development).ToList();
        var testProfiles = dataset.Profiles.Where(item => item.Split == EvaluationSplit.Test).ToList();

        Assert.Empty(developmentProfiles.Select(item => item.FamilyId)
            .Intersect(testProfiles.Select(item => item.FamilyId), StringComparer.Ordinal));
        Assert.Empty(dataset.Questions.Where(item => item.Split == EvaluationSplit.Development)
            .Select(item => item.TemplateFamilyId)
            .Intersect(dataset.Questions.Where(item => item.Split == EvaluationSplit.Test)
                .Select(item => item.TemplateFamilyId), StringComparer.Ordinal));
        Assert.Empty(dataset.Jobs.Where(item => item.Split == EvaluationSplit.Development)
            .Select(item => item.TemplateFamilyId)
            .Intersect(dataset.Jobs.Where(item => item.Split == EvaluationSplit.Test)
                .Select(item => item.TemplateFamilyId), StringComparer.Ordinal));
        Assert.Equal(dataset.QuestionFamilies.Count,
            dataset.QuestionFamilies.Select(item => item.TemplateFamilyId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(dataset.JobFamilies.Count,
            dataset.JobFamilies.Select(item => item.TemplateFamilyId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ExpandedValidatorRejectsProfileFamilyLeakage()
    {
        var dataset = ExpandedEvaluationDatasetFile.Load(DatasetPath()).Dataset;
        var developmentFamily = dataset.Profiles.First(item => item.Split == EvaluationSplit.Development).FamilyId;
        var testIndex = dataset.Profiles.FindIndex(item => item.Split == EvaluationSplit.Test);
        dataset.Profiles[testIndex] = dataset.Profiles[testIndex] with { FamilyId = developmentFamily };

        var error = Assert.Throws<InvalidDataException>(() => ExpandedEvaluationDatasetValidator.Validate(dataset));

        Assert.Contains("family", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExpandedValidatorRejectsActualTemplateFamilyLeakage()
    {
        var dataset = ExpandedEvaluationDatasetFile.Load(DatasetPath()).Dataset;
        var development = dataset.QuestionFamilies.First(item => item.Split == EvaluationSplit.Development);
        var testIndex = dataset.QuestionFamilies.FindIndex(item => item.Split == EvaluationSplit.Test);
        dataset.QuestionFamilies[testIndex] = dataset.QuestionFamilies[testIndex] with
        {
            TemplateFamilyId = development.TemplateFamilyId
        };

        var error = Assert.Throws<InvalidDataException>(() => ExpandedEvaluationDatasetValidator.Validate(
            ExpandedEvaluationDatasetValidator.Materialize(dataset)));

        Assert.Contains("template", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExpandedB0RunReportsActualCountsAndLeavesB1B2NotRun()
    {
        var report = ExpandedFixtureEvaluationRunner.RunFile(DatasetPath(), Options());

        Assert.Equal(12, report.ProfileGroupCount);
        Assert.Equal(60, report.JobCaseCount);
        Assert.Equal(240, report.QuestionCaseCount);
        Assert.Equal(EvaluationRunStatus.Completed, report.B0Status);
        Assert.Equal(EvaluationRunStatus.NotRun, report.B1Status);
        Assert.Equal(EvaluationRunStatus.NotRun, report.B2Status);
        Assert.Equal(300, report.Results.Count);
        Assert.True(report.Results.All(item => item.ExpectedOutcomeMatched),
            string.Join(", ", report.Results.Where(item => !item.ExpectedOutcomeMatched)
                .Select(item => $"{item.CaseId}={item.ActualStatus}, valueMatched={item.ValueMatched}")));
        AssertMetricIsPerfect(report, "b0-question-outcome", 240);
        AssertMetricIsPerfect(report, "b0-answerable-correct", 110);
        AssertMetricIsPerfect(report, "b0-appropriate-abstention", 130);
        AssertMetricIsPerfect(report, "b0-job-outcome", 60);
        Assert.Matches("^[A-F0-9]{64}$", report.DatasetSha256);
        Assert.Equal("test-revision", report.CodeRevision);
        Assert.Equal(ScenarioAsOf, report.ScenarioAsOf);
        Assert.Equal(RunAt, report.RunAt);
        Assert.Equal("fixture-b0", report.ModeId);
        Assert.Equal("deterministic-rules-v1", report.ModelId);
        Assert.Equal("fixture-rules-v1", report.PromptId);
    }

    [Fact]
    public void ExpandedReportJsonOmitsExpectedAndActualAnswerValues()
    {
        var report = ExpandedFixtureEvaluationRunner.RunFile(DatasetPath(), Options());

        var json = ExpandedEvaluationJson.Serialize(report);

        Assert.DoesNotContain("candidate@example.invalid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("100000", json, StringComparison.Ordinal);
        Assert.Contains("\"b1Status\": \"NotRun\"", json, StringComparison.Ordinal);
        Assert.Contains("\"b2Status\": \"NotRun\"", json, StringComparison.Ordinal);
    }

    private static void AssertMetricIsPerfect(ExpandedEvaluationReport report, string id, int denominator)
    {
        var metric = Assert.Single(report.Metrics, item => item.Id == id);
        Assert.Equal(denominator, metric.Numerator);
        Assert.Equal(denominator, metric.Denominator);
    }

    private static EvaluationRunOptions Options() => new(
        CodeRevision: "test-revision",
        ScenarioAsOf: ScenarioAsOf,
        RunAt: RunAt,
        ModeId: "fixture-b0",
        ModelId: "deterministic-rules-v1",
        PromptId: "fixture-rules-v1");

    private static string DatasetPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobAgent.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found."),
            "evals", "datasets", "expanded-synthetic-v1.json");
    }
}
