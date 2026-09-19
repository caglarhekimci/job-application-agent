using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobAgent.Core.Evaluation;

public static class ExpandedEvaluationDatasetFile
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static LoadedExpandedEvaluationDataset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = File.ReadAllBytes(path);
        var serialized = JsonSerializer.Deserialize<ExpandedEvaluationDataset>(bytes, Options)
            ?? throw new InvalidDataException("Expanded evaluation dataset is empty.");
        var dataset = ExpandedEvaluationDatasetValidator.Materialize(serialized);
        ExpandedEvaluationDatasetValidator.Validate(dataset);
        return new(dataset, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public static class ExpandedEvaluationDatasetValidator
{
    public static ExpandedEvaluationDataset Materialize(ExpandedEvaluationDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        var questions = new List<ExpandedQuestionCase>();
        var jobs = new List<ExpandedJobCase>();
        foreach (var profile in dataset.Profiles)
        {
            foreach (var family in dataset.QuestionFamilies)
            {
                if (family.Split != profile.Split) continue;
                if (!family.ExpectedByArchetype.TryGetValue(profile.Archetype, out var expected)) continue;
                questions.Add(new()
                {
                    Id = $"{profile.Id}--question--{family.Scenario}",
                    Synthetic = true,
                    Split = profile.Split,
                    ProfileId = profile.Id,
                    TemplateFamilyId = family.TemplateFamilyId,
                    Scenario = family.Scenario,
                    Expected = expected,
                    RuleRationale = family.RuleRationale
                });
            }
            foreach (var family in dataset.JobFamilies)
            {
                if (family.Split != profile.Split) continue;
                if (!family.ExpectedByArchetype.TryGetValue(profile.Archetype, out var expected)) continue;
                jobs.Add(new()
                {
                    Id = $"{profile.Id}--job--{family.Scenario}",
                    Synthetic = true,
                    Split = profile.Split,
                    ProfileId = profile.Id,
                    TemplateFamilyId = family.TemplateFamilyId,
                    Scenario = family.Scenario,
                    Expected = expected,
                    RuleRationale = family.RuleRationale
                });
            }
        }
        return dataset with { Questions = questions, Jobs = jobs };
    }

    public static void Validate(ExpandedEvaluationDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        if (dataset.SchemaVersion != "1.0") throw Invalid("schemaVersion must be 1.0");
        Required(dataset.DatasetId, "datasetId");
        Required(dataset.License, "license");
        Required(dataset.Scope, "scope");
        if (!dataset.Synthetic) throw Invalid("dataset must declare synthetic=true");
        if (dataset.Profiles.Count != 12) throw Invalid("dataset requires exactly 12 profile groups");
        if (dataset.QuestionFamilies.Count != 40 ||
            dataset.QuestionFamilies.Count(item => item.Split == EvaluationSplit.Development) != 20 ||
            dataset.QuestionFamilies.Count(item => item.Split == EvaluationSplit.Test) != 20)
            throw Invalid("dataset requires 20 question families in each split");
        if (dataset.JobFamilies.Count != 10 ||
            dataset.JobFamilies.Count(item => item.Split == EvaluationSplit.Development) != 5 ||
            dataset.JobFamilies.Count(item => item.Split == EvaluationSplit.Test) != 5)
            throw Invalid("dataset requires 5 job families in each split");
        if (dataset.Profiles.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 12 ||
            dataset.Profiles.Select(item => item.GroupId).Distinct(StringComparer.Ordinal).Count() != 12 ||
            dataset.Profiles.Select(item => item.Archetype).Distinct().Count() != 12)
            throw Invalid("profile ids, groups, and archetypes must be unique");
        if (!dataset.Profiles.Any(item => item.Split == EvaluationSplit.Development) ||
            !dataset.Profiles.Any(item => item.Split == EvaluationSplit.Test))
            throw Invalid("both development and test profile splits are required");
        foreach (var profile in dataset.Profiles)
        {
            Required(profile.Id, "profile id");
            Required(profile.GroupId, $"profile {profile.Id} groupId");
            Required(profile.FamilyId, $"profile {profile.Id} familyId");
            if (!profile.Synthetic) throw Invalid($"profile {profile.Id} must declare synthetic=true");
        }
        RejectSplitLeakage(dataset.Profiles, item => item.FamilyId, "profile family");

        var developmentArchetypes = dataset.Profiles.Where(item => item.Split == EvaluationSplit.Development)
            .Select(item => item.Archetype).ToHashSet();
        var testArchetypes = dataset.Profiles.Where(item => item.Split == EvaluationSplit.Test)
            .Select(item => item.Archetype).ToHashSet();
        ValidateFamilies(dataset.QuestionFamilies.Select(item => new FamilyView(
            item.Synthetic, item.Split, item.Scenario.ToString(), item.TemplateFamilyId,
            item.RuleRationale, item.ExpectedByArchetype.Keys.ToHashSet())),
            developmentArchetypes, testArchetypes, "question");
        ValidateFamilies(dataset.JobFamilies.Select(item => new FamilyView(
            item.Synthetic, item.Split, item.Scenario.ToString(), item.TemplateFamilyId,
            item.RuleRationale, item.ExpectedByArchetype.Keys.ToHashSet())),
            developmentArchetypes, testArchetypes, "job");
        if (dataset.Questions.Count != 240 || dataset.Jobs.Count != 60)
            throw Invalid("materialized dataset must contain 240 questions and 60 jobs");
        if (dataset.Questions.Any(item => !item.Synthetic || string.IsNullOrWhiteSpace(item.Expected.Status) ||
            string.IsNullOrWhiteSpace(item.RuleRationale)) ||
            dataset.Jobs.Any(item => !item.Synthetic || string.IsNullOrWhiteSpace(item.Expected.Status) ||
            string.IsNullOrWhiteSpace(item.RuleRationale)))
            throw Invalid("every materialized case requires synthetic=true, expected status, and rationale");
    }

    private static void ValidateFamilies(IEnumerable<FamilyView> families,
        HashSet<ExpandedProfileArchetype> developmentArchetypes,
        HashSet<ExpandedProfileArchetype> testArchetypes, string label)
    {
        var list = families.ToList();
        if (list.Select(item => item.Scenario).Distinct(StringComparer.Ordinal).Count() != list.Count)
            throw Invalid($"{label} scenarios must be unique");
        if (list.Select(item => item.Template).Distinct(StringComparer.Ordinal).Count() != list.Count)
            throw Invalid($"development/test {label} template family leakage");
        foreach (var family in list)
        {
            if (!family.Synthetic) throw Invalid($"{label} family {family.Scenario} must declare synthetic=true");
            Required(family.Template, $"{label} family template");
            Required(family.Rationale, $"{label} family rationale");
            var expectedArchetypes = family.Split == EvaluationSplit.Development
                ? developmentArchetypes : testArchetypes;
            if (!family.Archetypes.SetEquals(expectedArchetypes))
                throw Invalid($"{label} family {family.Scenario} requires an expectation for every archetype in its split");
        }
    }

    private static void RejectSplitLeakage(IEnumerable<ExpandedProfileFixture> profiles,
        Func<ExpandedProfileFixture, string> selector, string label)
    {
        var development = profiles.Where(item => item.Split == EvaluationSplit.Development)
            .Select(selector).ToHashSet(StringComparer.Ordinal);
        development.IntersectWith(profiles.Where(item => item.Split == EvaluationSplit.Test).Select(selector));
        if (development.Count != 0) throw Invalid($"development/test {label} leakage");
    }

    private static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Invalid($"{name} is required");
    }

    private static InvalidDataException Invalid(string message) =>
        new($"Invalid expanded evaluation dataset: {message}.");

    private sealed record FamilyView(bool Synthetic, EvaluationSplit Split, string Scenario,
        string Template, string Rationale, HashSet<ExpandedProfileArchetype> Archetypes);
}
