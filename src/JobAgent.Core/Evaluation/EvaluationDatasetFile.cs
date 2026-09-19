using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobAgent.Core.Evaluation;

public static class EvaluationDatasetFile
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static LoadedEvaluationDataset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = File.ReadAllBytes(path);
        var dataset = JsonSerializer.Deserialize<EvaluationDataset>(bytes, Options)
            ?? throw new InvalidDataException("Evaluation dataset is empty.");
        EvaluationDatasetValidator.Validate(dataset);
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

public static class EvaluationDatasetValidator
{
    public static void Validate(EvaluationDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        if (dataset.SchemaVersion != "1.0") throw Invalid("schemaVersion must be 1.0");
        Required(dataset.DatasetId, "datasetId");
        Required(dataset.License, "license");
        Required(dataset.Scope, "scope");
        if (!dataset.Synthetic) throw Invalid("dataset must declare synthetic=true");
        if (dataset.Cases.Count == 0) throw Invalid("dataset must contain cases");
        if (dataset.Cases.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != dataset.Cases.Count)
            throw Invalid("case ids must be unique");

        foreach (var item in dataset.Cases)
        {
            Required(item.Id, "case id");
            Required(item.GroupId, $"case {item.Id} groupId");
            Required(item.TemplateId, $"case {item.Id} templateId");
            Required(item.RuleRationale, $"case {item.Id} ruleRationale");
            if (!item.Synthetic) throw Invalid($"case {item.Id} must declare synthetic=true");
            if (item.Kind == EvaluationCaseKind.Answer && item.Expected.Status is null)
                throw Invalid($"answer case {item.Id} requires expected status");
            if (item.Kind == EvaluationCaseKind.Job && item.Expected.Status is null)
                throw Invalid($"job case {item.Id} requires expected status");
            if (item.Kind == EvaluationCaseKind.Submission && item.Expected.Decision is null)
                throw Invalid($"submission case {item.Id} requires expected decision");
        }

        RejectSplitLeakage(dataset, item => item.TemplateId, "template");
        RejectSplitLeakage(dataset, item => item.GroupId, "group");
    }

    private static void RejectSplitLeakage(EvaluationDataset dataset,
        Func<EvaluationCase, string> selector, string label)
    {
        var development = dataset.Cases.Where(item => item.Split == EvaluationSplit.Development)
            .Select(selector).ToHashSet(StringComparer.Ordinal);
        var test = dataset.Cases.Where(item => item.Split == EvaluationSplit.Test)
            .Select(selector).ToHashSet(StringComparer.Ordinal);
        development.IntersectWith(test);
        if (development.Count != 0)
            throw Invalid($"development/test {label} leakage: {string.Join(", ", development.Order())}");
    }

    private static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Invalid($"{name} is required");
    }

    private static InvalidDataException Invalid(string message) => new($"Invalid evaluation dataset: {message}.");
}
