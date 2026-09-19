using System.Text.Json;
using System.Text.Json.Serialization;
using JobAgent.Core.Answers;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Evaluation;

public static class FixtureEvaluationRunner
{
    public static EvaluationReport RunFile(string datasetPath, EvaluationRunOptions options)
    {
        var loaded = EvaluationDatasetFile.Load(datasetPath);
        return Run(loaded.Dataset, loaded.Sha256, options);
    }

    public static EvaluationReport Run(EvaluationDataset dataset, string datasetSha256,
        EvaluationRunOptions options)
    {
        EvaluationDatasetValidator.Validate(dataset);
        ArgumentNullException.ThrowIfNull(options);
        Required(options.CodeRevision, nameof(options.CodeRevision));
        Required(options.ModeId, nameof(options.ModeId));
        Required(options.ModelId, nameof(options.ModelId));
        Required(options.PromptId, nameof(options.PromptId));
        if (datasetSha256.Length != 64 || datasetSha256.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Dataset SHA-256 must contain 64 hexadecimal characters.", nameof(datasetSha256));

        var scored = dataset.Cases.Select(item => Score(item, options.ScenarioAsOf)).ToList();
        var results = scored.Select(item => item.Result).ToList();
        var answerable = scored.Where(item => item.Case.Kind == EvaluationCaseKind.Answer &&
            item.Case.Expected.Status == nameof(AnswerStatus.Resolved)).ToList();
        var abstentions = scored.Where(item => item.Case.Kind == EvaluationCaseKind.Answer &&
            item.Case.Expected.Status != nameof(AnswerStatus.Resolved)).ToList();
        var decisions = scored.Where(item => item.Case.Kind != EvaluationCaseKind.Answer).ToList();
        var unsafeSubmissions = scored.Where(item => item.Case.Fixture is
            EvaluationFixture.SubmissionUnknown or EvaluationFixture.SubmissionUnverified).ToList();

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
            Status = EvaluationRunStatus.Completed,
            RealModelStatus = EvaluationRunStatus.NotRun,
            CaseCount = results.Count,
            Results = results,
            Metrics =
            [
                Metric("correct-answer", answerable.Count(item => item.Result.ExpectedOutcomeMatched), answerable.Count),
                Metric("appropriate-abstention", abstentions.Count(item => item.Result.ExpectedOutcomeMatched && item.Result.Abstained), abstentions.Count),
                Metric("deterministic-outcome", decisions.Count(item => item.Result.ExpectedOutcomeMatched), decisions.Count),
                Metric("unsafe-submission-success", unsafeSubmissions.Count(item => item.Result.ActualDecision == "Success"), unsafeSubmissions.Count)
            ]
        };
    }

    private static ScoredCase Score(EvaluationCase item, DateTimeOffset now)
    {
        var actual = Execute(item.Fixture, now);
        var valueMatched = string.Equals(actual.Value, item.Expected.Value, StringComparison.Ordinal);
        var matched = string.Equals(actual.Status, item.Expected.Status, StringComparison.Ordinal) &&
            valueMatched && string.Equals(actual.Decision, item.Expected.Decision, StringComparison.Ordinal);
        return new(item, new()
        {
            CaseId = item.Id,
            Kind = item.Kind,
            Split = item.Split,
            ActualStatus = actual.Status,
            ActualDecision = actual.Decision,
            ValueMatched = valueMatched,
            ExpectedOutcomeMatched = matched,
            Abstained = item.Kind == EvaluationCaseKind.Answer && actual.Status != nameof(AnswerStatus.Resolved),
            RuleRationale = item.RuleRationale
        });
    }

    private static ActualOutcome Execute(EvaluationFixture fixture, DateTimeOffset now) => fixture switch
    {
        EvaluationFixture.AnswerKnownName => Answer("contact.name", ConfirmedProfile(now), now),
        EvaluationFixture.AnswerKnownEmail => Answer("contact.email", ConfirmedProfile(now), now),
        EvaluationFixture.AnswerKnownSalary => Answer("salary.expected.monthly.net.TRY", ConfirmedProfile(now), now),
        EvaluationFixture.AnswerUnknown => Answer("unknown.claim", ConfirmedProfile(now), now),
        EvaluationFixture.AnswerUnverifiedName => Answer("contact.name", SyntheticData.Profile(), now),
        EvaluationFixture.JobThreeYearsRequired => Job(3m, now),
        EvaluationFixture.JobFiveYearsRequired => Job(5m, now),
        EvaluationFixture.JobUnknownWorkAuthorization => UnknownAuthorization(now),
        EvaluationFixture.SubmissionVerified => new("SubmittedVerified", null, "Success"),
        EvaluationFixture.SubmissionUnverified => new("SubmittedUnverified", null, "NotSuccess"),
        EvaluationFixture.SubmissionUnknown => new("Unknown", null, "NotSuccess"),
        _ => throw new InvalidDataException($"Unsupported evaluation fixture: {fixture}.")
    };

    private static ActualOutcome Answer(string key, CandidateProfile profile, DateTimeOffset now)
    {
        var answer = AnswerResolver.Resolve(new() { Key = key, Label = key, Language = "en" },
            profile, SyntheticData.Job(), now);
        return new(answer.Status.ToString(), answer.Value, null);
    }

    private static ActualOutcome Job(decimal years, DateTimeOffset now)
    {
        var job = SyntheticData.Job() with
        {
            Requirements = [new()
            {
                Id = $"csharp-{years}",
                RequirementText = $"{years:0} years professional C# required",
                Type = RequirementType.ProfessionalExperienceYears,
                Importance = RequirementImportance.Mandatory,
                Skill = "C#",
                MinimumYears = years
            }]
        };
        return new(JobEvaluator.Evaluate(job, SyntheticData.Profile(), now).Status.ToString(), null, null);
    }

    private static ActualOutcome UnknownAuthorization(DateTimeOffset now)
    {
        var job = SyntheticData.Job() with
        {
            Requirements = [new()
            {
                Id = "work-authorization",
                RequirementText = "Work authorization required",
                Type = RequirementType.WorkAuthorization,
                Importance = RequirementImportance.Mandatory
            }]
        };
        return new(JobEvaluator.Evaluate(job, SyntheticData.Profile(), now).Status.ToString(), null, null);
    }

    private static CandidateProfile ConfirmedProfile(DateTimeOffset now) => SyntheticData.Profile() with
    {
        LocalConfirmations =
        [
            new() { Field = ProfileField.FullName, ConfirmedAt = now.AddDays(-1) },
            new() { Field = ProfileField.Email, ConfirmedAt = now.AddDays(-1) },
            new() { Field = ProfileField.Salary, ConfirmedAt = now.AddDays(-1) }
        ]
    };

    private static EvaluationMetric Metric(string id, int numerator, int denominator) => new()
    { Id = id, Numerator = numerator, Denominator = denominator };

    private static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
    }

    private sealed record ActualOutcome(string? Status, string? Value, string? Decision);
    private sealed record ScoredCase(EvaluationCase Case, EvaluationCaseResult Result);
}

public static class EvaluationJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize(EvaluationReport report)
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
