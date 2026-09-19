using JobAgent.Core.Answers;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Core.Applications;

public sealed record QuestionAnswer(FormQuestion Question, AnswerResolution Answer);
public sealed record ApplicationQuestionReview(ApplicationDraft Draft, IReadOnlyList<QuestionAnswer> Questions);

// Common pure rules for protected personal drafts and the authorized synthetic adapter.
// Callers persist the question snapshot before displaying NeedsInput and clear stored approvals
// when applying the returned package. This type grants no permission and performs no I/O.
public static class ApplicationQuestions
{
    public static ApplicationQuestionReview Resolve(ApplicationDraft draft, IReadOnlyList<FormQuestion> questions,
        CandidateProfile profile, JobPosting job, DateTimeOffset now)
    {
        EnsureEditable(draft, profile, job);
        ValidateQuestions(questions);
        var context = new AnswerScopeContext(draft.Id, job.RoleGroupId);
        var resolutions = questions.Select(q => new QuestionAnswer(q,
            AnswerResolver.Resolve(q, profile, job, now, context))).ToList();
        var answers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in resolutions.Where(item => item.Answer.Status == AnswerStatus.Resolved))
            if (item.Answer.Value is { } value) answers.Add(item.Question.Key, value);
        return new(draft with
        {
            ProfileVersion = profile.Version,
            Questions = [.. questions],
            Answers = answers,
            Status = resolutions.All(item => item.Answer.Status == AnswerStatus.Resolved && item.Answer.Value is not null)
                ? ApplicationStatus.ReadyForDataSharing : ApplicationStatus.NeedsInput
        }, resolutions);
    }

    public static CandidateProfile RememberReviewed(ApplicationDraft draft, CandidateProfile profile,
        JobPosting job, ReviewedAnswerMemoryUpdate update, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(update);
        EnsureEditable(draft, profile, job);
        var question = draft.Questions.SingleOrDefault(q =>
            string.Equals(q.Key, update.SemanticKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(q.Language, update.Language, StringComparison.OrdinalIgnoreCase))
            ?? throw new PolicyException("QuestionNotFound");
        if (question.Sensitive || question.RequiresCandidateAttestation)
            throw new PolicyException("ManualAnswerNotAutomatable");
        if (update.Answer is null) throw new ArgumentException("A reviewed answer is required.", nameof(update));
        if (question.MaxLength is { } limit && update.Answer.Length > limit)
            throw new ArgumentException("Answer exceeds the reviewed field limit.");
        var scopeId = update.Scope switch
        {
            AnswerScopeType.Default => null,
            AnswerScopeType.Application => draft.Id.ToString("D"),
            AnswerScopeType.Company => job.Employer,
            AnswerScopeType.RoleGroup => job.RoleGroupId,
            _ => throw new ArgumentException("Invalid answer scope.")
        };
        return AnswerMemoryService.UpsertReviewed(profile, update with { ScopeId = scopeId }, now);
    }

    public static void ValidateQuestions(IReadOnlyList<FormQuestion> questions)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count is < 1 or > 100 || questions.Any(q => q is null) ||
            questions.Select(q => q.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != questions.Count ||
            questions.Any(q => string.IsNullOrWhiteSpace(q.Key) || q.Key.Length > 200 || q.Key != q.Key.Trim() ||
                string.IsNullOrWhiteSpace(q.Label) || q.Label.Length > 2000 ||
                string.IsNullOrWhiteSpace(q.Language) || q.Language.Length > 32 || q.MaxLength is < 1 or > 4000))
            throw new ArgumentException("Question set is invalid or ambiguous.");
    }

    private static void EnsureEditable(ApplicationDraft draft, CandidateProfile profile, JobPosting job)
    {
        if (draft.ProfileId != profile.Id || draft.JobKey != job.Id) throw new PolicyException("ApplicationReferencesChanged");
        if (draft.Status is ApplicationStatus.Submitting or ApplicationStatus.SubmittedVerified or
            ApplicationStatus.SubmittedUnverified or ApplicationStatus.Cancelled)
            throw new PolicyException("ApplicationNotEditable");
    }
}
