using JobAgent.Core.Profiles;

namespace JobAgent.Core.Jobs;

public static class JobEvaluator
{
    public static JobEvaluation Evaluate(JobPosting job, CandidateProfile profile, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(profile);
        if (job.Availability == JobAvailability.Closed)
            return new() { Status = JobEvaluationStatus.Closed, Reason = "The job is closed." };
        if (job.Requirements.Count == 0)
            return new()
            {
                Status = JobEvaluationStatus.InsufficientInformation,
                Reason = "No reviewed requirements are available."
            };

        var results = job.Requirements.Select(requirement => Assess(requirement, profile, now)).ToList();
        var status = DetermineStatus(job.Requirements, results);
        return new() { Status = status, Requirements = results, Reason = "Requirements were evaluated deterministically." };
    }

    private static RequirementResult Assess(JobRequirement requirement, CandidateProfile profile, DateTimeOffset now)
    {
        if (requirement.ReviewStatus != RequirementReviewStatus.Confirmed)
            return Result(requirement, RequirementAssessment.Unknown, "The requirement has not been confirmed by the user.");
        if (requirement.Type == RequirementType.WorkAuthorization)
            return Result(requirement, RequirementAssessment.Unknown, "Work authorization is not recorded.");

        if (requirement.Type != RequirementType.ProfessionalExperienceYears ||
            requirement.MinimumYears is null || string.IsNullOrWhiteSpace(requirement.Skill))
            return Result(requirement, RequirementAssessment.Unknown, "No deterministic evaluator exists for this requirement.");

        var verified = ProfilePolicy.UsableVerifiedFacts(profile, now).Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var periods = profile.Experience
            .Where(period => period.Kind == ExperienceKind.Professional)
            .Where(period => period.Skills.Any(skill => skill.Equals(requirement.Skill, StringComparison.OrdinalIgnoreCase)))
            .Where(period => period.EvidenceIds.Any(verified.Contains))
            .ToList();
        if (periods.Count == 0)
            return Result(requirement, RequirementAssessment.Unknown, "No verified professional evidence exists.");

        var currentDate = DateOnly.FromDateTime(now.UtcDateTime);
        var intervals = periods
            .Where(period => period.Start < currentDate)
            .Select(period => (period.Start, End: period.End is null || period.End > currentDate ? currentDate : period.End.Value))
            .OrderBy(period => period.Start)
            .ToList();
        var merged = new List<(DateOnly Start, DateOnly End)>();
        foreach (var interval in intervals)
        {
            if (merged.Count == 0 || interval.Start > merged[^1].End.AddDays(1))
                merged.Add(interval);
            else if (interval.End > merged[^1].End)
                merged[^1] = (merged[^1].Start, interval.End);
        }
        var days = merged.Sum(period => (decimal)(period.End.DayNumber - period.Start.DayNumber));
        var years = days / 365m;
        var evidence = periods.SelectMany(period => period.EvidenceIds).Where(verified.Contains).Distinct(StringComparer.Ordinal).ToList();
        return years >= requirement.MinimumYears
            ? Result(requirement, RequirementAssessment.Match, $"Verified professional experience is {years:0.#} years.", evidence)
            : Result(requirement, RequirementAssessment.Mismatch, $"Verified professional experience is {years:0.#} years.", evidence);
    }

    private static JobEvaluationStatus DetermineStatus(IReadOnlyList<JobRequirement> requirements, IReadOnlyList<RequirementResult> results)
    {
        if (results.Select((result, index) => (result, requirements[index])).Any(item =>
            item.Item2.Importance == RequirementImportance.Mandatory && item.result.Assessment == RequirementAssessment.Mismatch))
            return JobEvaluationStatus.HardRequirementMismatch;
        if (results.Select((result, index) => (result, requirements[index])).Any(item =>
            item.Item2.Importance == RequirementImportance.Mandatory && item.result.Assessment == RequirementAssessment.Unknown))
            return JobEvaluationStatus.InsufficientInformation;
        if (results.Any(result => result.Assessment is RequirementAssessment.Mismatch or RequirementAssessment.Unknown))
            return JobEvaluationStatus.ReviewNeeded;
        return JobEvaluationStatus.Eligible;
    }

    private static RequirementResult Result(JobRequirement requirement, RequirementAssessment assessment, string reason, List<string>? evidence = null) => new()
    {
        RequirementText = requirement.RequirementText,
        RequirementType = requirement.Type,
        Assessment = assessment,
        Reason = reason,
        CandidateEvidenceIds = evidence ?? []
    };
}
