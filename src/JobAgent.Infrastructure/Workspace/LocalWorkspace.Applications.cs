using System.Net.Mail;
using System.Text.Json;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;

namespace JobAgent.Infrastructure.Workspace;

internal sealed record WorkspaceApplication(ApplicationDraft Draft, JobPosting Job,
    Guid ResumeRef, string? ReviewHash = null, List<AnswerProposal>? Proposals = null,
    int? ProposalProfileVersion = null);
internal sealed record WorkspaceProfileProposal(Guid Id, HostProfilePatchRequest Request);

internal sealed partial record WorkspaceData
{
    public List<WorkspaceApplication> Applications { get; init; } = [];
    public List<WorkspaceProfileProposal> ProfileProposals { get; init; } = [];
    public List<JobImportProposal> JobProposals { get; init; } = [];
    public Dictionary<Guid, List<FormQuestion>> QuestionTemplates { get; init; } = [];
}

// UI and optional local MCP host share this protected, revision-bound workspace.
// All host proposals remain pending. There is deliberately no browser/approval dependency.
public sealed partial class LocalWorkspace : ILocalWorkspaceHost
{
    private static readonly List<FormQuestion> DefaultQuestions =
    [
        new() { Key = "contact.name", Label = "Ad soyad", Language = "tr" },
        new() { Key = "contact.email", Label = "E-posta", Language = "tr" },
        new() { Key = "experience.professional.csharp.years", Label = "Profesyonel C# deneyimi (yıl)", Language = "tr" },
        new() { Key = "motivation", Label = "Bu rolü neden istiyorsunuz?", Language = "tr", MaxLength = 2000 }
    ];

    public Task<HostWorkspaceRefs> GetHostWorkspaceRefsAsync() => WithWorkspace(async (revision, data) =>
    {
        await Task.CompletedTask;
        return References(revision, data);
    });

    public Task<HostProfileSummary> GetHostProfileSummaryAsync(Guid profileRef) => WithWorkspace(async (_, data) =>
    {
        RequireProfile(data, profileRef);
        await Task.CompletedTask;
        var facts = HostEvidence(data.Profile);
        return new HostProfileSummary(profileRef, data.Profile.Version,
            facts.Select(f => f.Skill).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), facts);
    });

    public Task<HostPendingProposal> ProposeProfilePatchAsync(HostProfilePatchRequest request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireProfile(data, request.ProfileRef);
        if (request.BaseVersion != data.Profile.Version) throw new PolicyException("ProfileVersionConflict");
        if (request.Changes is null || request.Changes.Count is < 1 or > 20 || data.ProfileProposals.Count >= 20)
            throw new ArgumentException("Profile proposal limit exceeded.");
        var evidence = HostEvidence(data.Profile).Select(f => f.EvidenceRef).ToHashSet(StringComparer.Ordinal);
        foreach (var change in request.Changes)
        {
            if (change is null || !Enum.IsDefined(change.Field) || string.IsNullOrWhiteSpace(change.Value) ||
                change.Value.Length > 254 || change.EvidenceRefs is null || change.EvidenceRefs.Count > 20 ||
                change.EvidenceRefs.Any(id => !evidence.Contains(id)))
                throw new ArgumentException("Invalid profile proposal.");
            if (change.Field == HostProfileField.FullName && change.Value.Length > 200 ||
                change.Field == HostProfileField.Email && (!MailAddress.TryCreate(change.Value, out var mail) || mail.Address != change.Value) ||
                change.Field == HostProfileField.Locale && change.Value is not ("en" or "tr") ||
                change.Field == HostProfileField.ProfessionalSkill && (change.Value.Length > 80 || change.EvidenceRefs.Count == 0))
                throw new ArgumentException("Invalid field value or missing professional evidence.");
        }
        var id = Guid.NewGuid();
        await SaveAsync(revision, data with { ProfileProposals = [.. data.ProfileProposals, new(id, request)] });
        return new HostPendingProposal(id, revision + 1, request.BaseVersion, "Pending", true);
    });

    public Task<HostJobImportResult> ImportJobProposalAsync(HostJobImportRequest request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 100_000 ||
            request.SourceUrl?.Length > 2048 || request.Employer?.Length > 200 || request.Title?.Length > 300)
            throw new ArgumentException("Job proposal limit exceeded.");
        var id = Guid.NewGuid();
        var proposal = JobPostingImporter.ProposeProvidedText(id.ToString("D"),
            string.IsNullOrWhiteSpace(request.Employer) ? "Unspecified employer" : request.Employer,
            string.IsNullOrWhiteSpace(request.Title) ? "Unspecified role" : request.Title,
            request.Text, request.SourceUrl ?? "", DateTimeOffset.UtcNow);
        var existing = data.JobProposals.FirstOrDefault(item => SamePendingJob(item.Posting, proposal.Posting));
        if (existing is not null)
            return new HostJobImportResult(JobRef(existing.Posting), revision, true, existing.SuggestedRequirements);
        if (data.JobProposals.Count >= 20) throw new ArgumentException("Job proposal limit exceeded.");
        await SaveAsync(revision, data with { JobProposals = [.. data.JobProposals, proposal] });
        return new HostJobImportResult(id, revision + 1, true, proposal.SuggestedRequirements);
    });

    public Task<HostJobEvaluation> EvaluateJobAsync(Guid jobRef, Guid profileRef) => WithWorkspace(async (revision, data) =>
    {
        RequireProfile(data, profileRef);
        var job = FindJob(data, jobRef);
        var evaluation = job.RequirementsReviewedAt is null
            ? new JobEvaluation { Status = JobEvaluationStatus.ReviewNeeded, Reason = "Job requirements need local user review." }
            : job.Requirements.Count == 0
                ? new JobEvaluation { Status = JobEvaluationStatus.InsufficientInformation, Reason = "No reviewed requirements." }
                : JobEvaluator.Evaluate(job, data.Profile, DateTimeOffset.UtcNow);
        await Task.CompletedTask;
        return new HostJobEvaluation(jobRef, profileRef, data.Profile.Version, revision, evaluation);
    });

    public Task<HostApplicationState> CreateApplicationAsync(HostCreateApplicationRequest request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireProfile(data, request.ProfileRef);
        if (data.Profile.VerifiedAt is null) throw new PolicyException("ProfileReviewRequired");
        if (request.ResumeRef == Guid.Empty || request.ResumeRef != data.ResumeRef || data.Document is null)
            throw new PolicyException("ResumeReferenceChanged");
        var job = FindJob(data, request.JobRef);
        if (job.RequirementsReviewedAt is null || data.Job is null || JobRef(data.Job) != request.JobRef)
            throw new PolicyException("JobReviewRequired");
        var existing = data.Applications.SingleOrDefault(a => a.Draft.ProfileId == request.ProfileRef && a.Draft.JobKey == job.Id);
        if (existing is not null) return HostState(revision, Refresh(existing, data), data);
        if (data.Applications.Count >= 100) throw new PolicyException("ApplicationLimitReached");
        var application = BuildApplication(data, job, request.ResumeRef);
        await SaveAsync(revision, data with { Applications = [.. data.Applications, application] });
        return HostState(revision + 1, application, data);
    });

    private static WorkspaceApplication BuildApplication(WorkspaceData data, JobPosting job, Guid resumeRef)
    {
        var questions = data.QuestionTemplates.GetValueOrDefault(JobRef(job)) ?? DefaultQuestions;
        var draft = ApplicationQuestions.Resolve(new()
        {
            ProfileId = data.Profile.Id,
            ProfileVersion = data.Profile.Version,
            JobKey = job.Id,
            JobTitle = job.Title,
            Employer = job.Employer,
            RecipientOrigin = JobUrlCanonicalizer.Origin(job.CanonicalUrl),
            ResumeRef = resumeRef.ToString("D"),
            ResumeHash = data.Document!.FileHash,
            Synthetic = false
        }, questions, data.Profile, job, DateTimeOffset.UtcNow).Draft;
        return new WorkspaceApplication(draft, job, resumeRef);
    }

    public Task<HostApplicationQuestions> GetApplicationQuestionsAsync(Guid applicationRef) => WithWorkspace(async (revision, data) =>
    {
        var application = Refresh(FindApplication(data, applicationRef), data);
        var evidence = HostEvidence(data.Profile).Select(f => f.EvidenceRef).Distinct(StringComparer.Ordinal).Take(20).ToArray();
        var questions = QuestionResults(application, data).Where(q => q.Answer.Status != AnswerStatus.Resolved)
            .Select(q => new HostQuestionSummary(q.Question.Key, q.Question.Label, q.Question.Language, q.Question.MaxLength,
                q.Answer.Status, q.Question.Sensitive || q.Question.RequiresCandidateAttestation, evidence)).ToArray();
        await Task.CompletedTask;
        return new HostApplicationQuestions(applicationRef, revision, questions);
    });

    public Task<HostAnswerProposalResult> ProposeApplicationAnswersAsync(HostAnswerProposalRequest request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        if (revision != request.BaseRevision) throw new PolicyException("ApplicationRevisionConflict");
        var application = Refresh(FindApplication(data, request.ApplicationRef), data);
        RequireEditable(application);
        if (request.Answers is null || request.Answers.Count is < 1 or > 20 || request.EvidenceRefs is null ||
            request.EvidenceRefs.Count > 100 || request.EvidenceRefs.Any(e => string.IsNullOrWhiteSpace(e) || e.Length > 200) ||
            request.Answers.Any(a => a is null) ||
            request.Answers.Select(a => a.SemanticKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Answers.Count)
            throw new ArgumentException("Invalid answer proposal set.");
        var results = new List<HostAnswerProposalItem>();
        var pending = application.Proposals?.ToList() ?? [];
        foreach (var answer in request.Answers)
        {
            var question = application.Draft.Questions.SingleOrDefault(q => q.Key == answer.SemanticKey)
                ?? throw new PolicyException("QuestionNotFound");
            var validated = question.Key.StartsWith("salary.", StringComparison.OrdinalIgnoreCase)
                ? new AnswerProposalValidation { Disposition = AnswerProposalDisposition.Abstained, Reason = "Salary requires local profile review." }
                : ModelAnswerProposalValidator.ValidateJson(JsonSerializer.Serialize(answer, Json), question, data.Profile,
                    request.EvidenceRefs, DateTimeOffset.UtcNow);
            results.Add(new(answer.SemanticKey, validated.Disposition, validated.Reason));
            pending.RemoveAll(p => p.SemanticKey == answer.SemanticKey);
            if (validated.Proposal is { } proposal) pending.Add(proposal);
        }
        if (pending.Count > 100) throw new ArgumentException("Too many pending answer proposals.");
        var next = application with
        {
            Proposals = pending,
            ProposalProfileVersion = pending.Count == 0 ? null : data.Profile.Version,
            ReviewHash = null
        };
        await SaveAsync(revision, ReplaceApplication(data, next));
        return new HostAnswerProposalResult(request.ApplicationRef, revision + 1, results, true);
    });

    public Task<HostApplicationState> PrepareApplicationReviewAsync(Guid applicationRef) => WithWorkspace(async (revision, data) =>
    {
        var application = Refresh(FindApplication(data, applicationRef), data);
        RequireEditable(application);
        var next = application with { ReviewHash = application.Draft.PayloadHash() };
        await SaveAsync(revision, ReplaceApplication(data, next));
        return HostState(revision + 1, next, data);
    });

    public Task<HostApplicationState> ExecuteApprovedApplicationAsync(Guid applicationRef) => WithWorkspace<HostApplicationState>(async (_, data) =>
    {
        FindApplication(data, applicationRef);
        await Task.CompletedTask;
        // User-provided text is not platform permission; this workspace has no live adapter.
        throw new PolicyException("BlockedPermission");
    });

    public Task<HostApplicationState> GetApplicationStatusAsync(Guid applicationRef) => WithWorkspace(async (revision, data) =>
    {
        await Task.CompletedTask;
        return HostState(revision, Refresh(FindApplication(data, applicationRef), data), data);
    });

    public Task<HostApplicationState> CancelApplicationAsync(Guid applicationRef) => WithWorkspace(async (revision, data) =>
    {
        var current = FindApplication(data, applicationRef);
        if (current.Draft.Status == ApplicationStatus.Cancelled) return HostState(revision, current, data);
        RequireEditable(current);
        var next = current with { Draft = current.Draft with { Status = ApplicationStatus.Cancelled }, ReviewHash = null };
        await SaveAsync(revision, ReplaceApplication(data, next));
        return HostState(revision + 1, next, data);
    });

    public Task<WorkspaceApplicationView> CancelApplicationFromUiAsync(Guid applicationRef, long expectedRevision) =>
        WithWorkspace(async (revision, data) =>
        {
            CheckRevision(expectedRevision, revision);
            var current = FindApplication(data, applicationRef);
            if (current.Draft.Status == ApplicationStatus.Cancelled)
                return ApplicationView(revision, current, data);
            RequireEditable(current);
            var next = current with
            {
                Draft = current.Draft with { Status = ApplicationStatus.Cancelled },
                ReviewHash = null
            };
            var updated = ReplaceApplication(data, next);
            await SaveAsync(revision, updated);
            return ApplicationView(revision + 1, next, updated);
        });

    private async Task<T> WithWorkspace<T>(Func<long, WorkspaceData, Task<T>> action)
    {
        await gate.WaitAsync();
        try { var (revision, data) = await ReadAsync(); return await action(revision, data); }
        finally { gate.Release(); }
    }

    private static HostWorkspaceRefs References(long revision, WorkspaceData data) => new(revision,
        data.Document is null ? null : data.Profile.Id, data.Document is null ? null : data.Profile.Version,
        data.ResumeRef == Guid.Empty ? null : data.ResumeRef, data.Job is null ? null : JobRef(data.Job));
    private static Guid JobRef(JobPosting job) => Guid.TryParse(job.Id, out var id) && id != Guid.Empty
        ? id : throw new PolicyException("InvalidJobReference");
    private static IReadOnlyList<HostSkillEvidence> HostEvidence(CandidateProfile profile)
    {
        var verified = ProfilePolicy.UsableVerifiedFacts(profile, DateTimeOffset.UtcNow)
            .Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        return profile.Experience.Where(p => p.Kind == ExperienceKind.Professional)
            .SelectMany(p => p.EvidenceIds.Where(verified.Contains)
                .SelectMany(id => p.Skills.Select(skill => new HostSkillEvidence(id, skill))))
            .Distinct().Take(100).ToArray();
    }
    private static void RequireProfile(WorkspaceData data, Guid profileRef)
    {
        if (profileRef == Guid.Empty || data.Document is null || profileRef != data.Profile.Id)
            throw new PolicyException("ProfileNotFound");
    }
    private static JobPosting FindJob(WorkspaceData data, Guid id) =>
        id != Guid.Empty && data.Job is { } job && JobRef(job) == id ? job :
        data.JobProposals.Select(p => p.Posting).SingleOrDefault(j => JobRef(j) == id) ?? throw new PolicyException("JobNotFound");
    private static WorkspaceApplication FindApplication(WorkspaceData data, Guid id) =>
        data.Applications.SingleOrDefault(a => a.Draft.Id == id) ?? throw new PolicyException("ApplicationNotFound");
    private static void RequireEditable(WorkspaceApplication application)
    {
        if (application.Draft.Status is ApplicationStatus.Cancelled or ApplicationStatus.Submitting or
            ApplicationStatus.SubmittedVerified or ApplicationStatus.SubmittedUnverified)
            throw new PolicyException("ApplicationNotEditable");
    }
    private static WorkspaceData ReplaceApplication(WorkspaceData data, WorkspaceApplication application) =>
        data with { Applications = data.Applications.Select(a => a.Draft.Id == application.Draft.Id ? application : a).ToList() };
    private static bool SamePendingJob(JobPosting left, JobPosting right) =>
        string.Equals(left.Employer, right.Employer, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Title, right.Title, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.CanonicalUrl, right.CanonicalUrl, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.TextHash, right.TextHash, StringComparison.Ordinal);
    private static bool ReferencesCurrent(WorkspaceApplication application, WorkspaceData data) =>
        data.Document is not null && data.Profile.VerifiedAt is not null && data.Profile.Id == application.Draft.ProfileId &&
        data.ResumeRef == application.ResumeRef && data.Document.FileHash == application.Draft.ResumeHash &&
        (data.Job is not { } currentJob || currentJob.Id != application.Job.Id ||
            (currentJob.TextHash == application.Job.TextHash && currentJob.CanonicalUrl == application.Job.CanonicalUrl &&
             currentJob.Title == application.Job.Title && currentJob.Employer == application.Job.Employer &&
             JsonSerializer.Serialize(currentJob.Requirements, Json) == JsonSerializer.Serialize(application.Job.Requirements, Json)));
    private static WorkspaceApplication Refresh(WorkspaceApplication application, WorkspaceData data)
    {
        var referencesCurrent = ReferencesCurrent(application, data);
        var usableEvidence = ProfilePolicy.UsableVerifiedFacts(data.Profile, DateTimeOffset.UtcNow)
            .Select(fact => fact.Id).ToHashSet(StringComparer.Ordinal);
        var proposals = referencesCurrent && application.ProposalProfileVersion == data.Profile.Version
            ? application.Proposals?.Where(proposal => proposal.EvidenceIds is not null &&
                proposal.EvidenceIds.All(usableEvidence.Contains)).ToList() ?? []
            : [];
        if (application.Draft.Status is ApplicationStatus.Cancelled or ApplicationStatus.SubmittedVerified or
            ApplicationStatus.SubmittedUnverified or ApplicationStatus.Submitting)
            return application with
            {
                Proposals = proposals,
                ProposalProfileVersion = proposals.Count == 0 ? null : application.ProposalProfileVersion
            };
        if (!referencesCurrent)
            return application with
            {
                Draft = application.Draft with { Status = ApplicationStatus.NeedsInput },
                ReviewHash = null,
                Proposals = [],
                ProposalProfileVersion = null
            };
        var updated = ApplicationQuestions.Resolve(application.Draft, application.Draft.Questions, data.Profile,
            application.Job, DateTimeOffset.UtcNow).Draft;
        return application with
        {
            Draft = updated,
            ReviewHash = application.ReviewHash == updated.PayloadHash() ? application.ReviewHash : null,
            Proposals = proposals,
            ProposalProfileVersion = proposals.Count == 0 ? null : application.ProposalProfileVersion
        };
    }
    private static IReadOnlyList<QuestionAnswer> QuestionResults(WorkspaceApplication application, WorkspaceData data) =>
        application.Draft.Questions.Select(q => new QuestionAnswer(q, ReferencesCurrent(application, data)
            ? AnswerResolver.Resolve(q, data.Profile, application.Job, DateTimeOffset.UtcNow,
                new(application.Draft.Id, application.Job.RoleGroupId))
            : new AnswerResolution { Status = AnswerStatus.RequiresReview, Reason = "Profile or resume changed; review the source first." })).ToList();
    private static HostApplicationState HostState(long revision, WorkspaceApplication application, WorkspaceData data) =>
        new(application.Draft.Id, revision, application.Draft.Status,
            application.ReviewHash is not null && application.ReviewHash == application.Draft.PayloadHash(), false,
            application.ReviewHash is null ? null : "local-application:" + application.Draft.Id.ToString("D"),
            QuestionResults(application, data).Count(q => q.Answer.Status != AnswerStatus.Resolved));
}
