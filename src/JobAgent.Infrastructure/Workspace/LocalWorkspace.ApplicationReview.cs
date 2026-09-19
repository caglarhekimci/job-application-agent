using System.Text.RegularExpressions;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Jobs;

namespace JobAgent.Infrastructure.Workspace;

public sealed record WorkspaceApplicationCreate(long ExpectedRevision, IReadOnlyList<FormQuestion>? Questions, string RoleGroupId = "");
public sealed record WorkspaceApplicationAnswerReview(Guid ApplicationRef, long ExpectedRevision,
    string ExpectedPayloadHash, IReadOnlyList<ReviewedAnswerMemoryUpdate> Answers);
public sealed record WorkspaceApplicationQuestionReview(Guid ApplicationRef, long ExpectedRevision,
    IReadOnlyList<FormQuestion> Questions, string RoleGroupId = "");
public sealed record WorkspaceApplicationView(long WorkspaceRevision, ApplicationDraft Draft,
    IReadOnlyList<QuestionAnswer> Questions, bool ReviewRequested, bool SourcesCurrent,
    IReadOnlyList<AnswerProposal> Proposals)
{
    public string PayloadHash => Draft.PayloadHash();
    public string RoleGroupId { get; init; } = "";
}
public sealed record WorkspaceProfileProposalView(Guid Id, HostProfilePatchRequest Request, string Status);
public sealed record WorkspaceApplicationPanel(long Revision, HostWorkspaceRefs References,
    IReadOnlyList<WorkspaceApplicationView> Applications,
    IReadOnlyList<WorkspaceProfileProposalView> ProfileProposals,
    IReadOnlyList<JobImportProposal> JobProposals);

// These methods are mapped only under cookie/CSRF-protected UI routes, never MCP.
public sealed partial class LocalWorkspace
{
    public Task<WorkspaceApplicationPanel> GetApplicationPanelAsync() => WithWorkspace(async (revision, data) =>
    {
        await Task.CompletedTask;
        return Panel(revision, data);
    });

    public Task<WorkspaceApplicationView> CreateApplicationFromUiAsync(WorkspaceApplicationCreate request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRevision(request.ExpectedRevision, revision);
        RequireReviewedSources(data);
        ValidateRoleGroup(request.RoleGroupId);
        var questions = request.Questions ?? DefaultQuestions;
        ApplicationQuestions.ValidateQuestions(questions);
        var job = data.Job! with { RoleGroupId = request.RoleGroupId };
        var existing = data.Applications.SingleOrDefault(a => a.Draft.JobKey == job.Id && a.Draft.ProfileId == data.Profile.Id);
        if (existing is not null) return ApplicationView(revision, Refresh(existing, data), data);
        if (data.Applications.Count >= 100) throw new PolicyException("ApplicationLimitReached");
        var next = data with
        {
            Job = job,
            QuestionTemplates = new(data.QuestionTemplates) { [JobRef(job)] = [.. questions] }
        };
        var application = BuildApplication(next, job, data.ResumeRef);
        next = next with { Applications = [.. next.Applications, application] };
        await SaveAsync(revision, next);
        return ApplicationView(revision + 1, application, next);
    });

    public Task<WorkspaceApplicationView> ReviewApplicationAnswersFromUiAsync(WorkspaceApplicationAnswerReview request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRevision(request.ExpectedRevision, revision);
        var application = Refresh(FindApplication(data, request.ApplicationRef), data);
        RequireEditable(application);
        if (!ReferencesCurrent(application, data)) throw new PolicyException("ApplicationSourcesChanged");
        if (request.ExpectedPayloadHash != application.Draft.PayloadHash()) throw new PolicyException("PackageChanged");
        if (request.Answers is null || request.Answers.Count is < 1 or > 20 ||
            request.Answers.Any(a => a is null || string.IsNullOrWhiteSpace(a.SemanticKey) ||
                string.IsNullOrWhiteSpace(a.Language) || a.Answer is null) ||
            request.Answers.Select(a => a.SemanticKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Answers.Count)
            throw new ArgumentException("Invalid reviewed answer set.");
        var profile = data.Profile;
        foreach (var answer in request.Answers)
        {
            if (answer.SemanticKey.StartsWith("salary.", StringComparison.OrdinalIgnoreCase) ||
                answer.SemanticKey.StartsWith("contact.", StringComparison.OrdinalIgnoreCase) ||
                answer.SemanticKey.StartsWith("experience.", StringComparison.OrdinalIgnoreCase))
                throw new PolicyException("ProfileFieldReviewRequired");
            profile = ApplicationQuestions.RememberReviewed(application.Draft, profile, application.Job, answer, DateTimeOffset.UtcNow);
        }
        var draft = ApplicationQuestions.Resolve(application.Draft, application.Draft.Questions, profile,
            application.Job, DateTimeOffset.UtcNow).Draft;
        var next = data with { Profile = profile, PreviousVersions = [.. data.PreviousVersions, data.Profile] };
        var proposals = application.Proposals?.Where(p => !request.Answers.Any(a =>
            string.Equals(a.SemanticKey, p.SemanticKey, StringComparison.OrdinalIgnoreCase))).ToList();
        var updated = application with
        {
            Draft = draft,
            ReviewHash = null,
            Proposals = proposals,
            ProposalProfileVersion = proposals is { Count: > 0 } ? application.ProposalProfileVersion : null
        };
        next = ReplaceApplication(next, updated);
        await SaveAsync(revision, next);
        return ApplicationView(revision + 1, updated, next);
    });

    public Task<WorkspaceApplicationView> UpdateApplicationQuestionsFromUiAsync(WorkspaceApplicationQuestionReview request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRevision(request.ExpectedRevision, revision);
        ValidateRoleGroup(request.RoleGroupId);
        ApplicationQuestions.ValidateQuestions(request.Questions);
        var application = Refresh(FindApplication(data, request.ApplicationRef), data);
        RequireEditable(application);
        if (!ReferencesCurrent(application, data)) throw new PolicyException("ApplicationSourcesChanged");
        var job = application.Job with { RoleGroupId = request.RoleGroupId };
        var draft = ApplicationQuestions.Resolve(application.Draft, request.Questions, data.Profile, job, DateTimeOffset.UtcNow).Draft;
        var updated = application with { Draft = draft, Job = job, ReviewHash = null, Proposals = [], ProposalProfileVersion = null };
        var next = ReplaceApplication(data, updated) with
        { QuestionTemplates = new(data.QuestionTemplates) { [JobRef(job)] = [.. request.Questions] } };
        await SaveAsync(revision, next);
        return ApplicationView(revision + 1, updated, next);
    });

    public Task<WorkspaceApplicationView> RefreshApplicationSourcesFromUiAsync(Guid applicationRef, long expectedRevision) => WithWorkspace(async (revision, data) =>
    {
        CheckRevision(expectedRevision, revision);
        RequireReviewedSources(data);
        var application = FindApplication(data, applicationRef);
        RequireEditable(application);
        if (data.Job!.Id != application.Job.Id) throw new PolicyException("ReviewOriginalJobFirst");
        var job = data.Job with { RoleGroupId = application.Job.RoleGroupId };
        var draft = ApplicationQuestions.Resolve(application.Draft with
        {
            ProfileVersion = data.Profile.Version,
            ResumeRef = data.ResumeRef.ToString("D"),
            ResumeHash = data.Document!.FileHash,
            Employer = job.Employer,
            JobTitle = job.Title,
            RecipientOrigin = JobUrlCanonicalizer.Origin(job.CanonicalUrl)
        }, application.Draft.Questions, data.Profile, job, DateTimeOffset.UtcNow).Draft;
        var updated = application with
        {
            Draft = draft,
            Job = job,
            ResumeRef = data.ResumeRef,
            ReviewHash = null,
            Proposals = [],
            ProposalProfileVersion = null
        };
        var next = ReplaceApplication(data, updated);
        await SaveAsync(revision, next);
        return ApplicationView(revision + 1, updated, next);
    });

    public Task<WorkspaceView> ReviewImportedJobFromUiAsync(Guid proposalRef, JobReview review) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(review);
        CheckRevision(review.ExpectedRevision, revision);
        var proposal = data.JobProposals.SingleOrDefault(p => JobRef(p.Posting) == proposalRef)
            ?? throw new PolicyException("JobProposalNotFound");
        if (string.IsNullOrWhiteSpace(review.Employer) || review.Employer.Length > 200 ||
            string.IsNullOrWhiteSpace(review.Title) || review.Title.Length > 300 ||
            string.IsNullOrWhiteSpace(review.Text) || review.Text.Length > 100000 || review.SourceUrl is null ||
            review.SourceUrl.Length > 2048 || review.Requirements is null || review.Requirements.Count > 100 ||
            review.Requirements.Any(requirement => requirement is null))
            throw new ArgumentException("Invalid reviewed job.");
        foreach (var requirement in review.Requirements)
        {
            if (!Enum.IsDefined(requirement.Type) || !Enum.IsDefined(requirement.Importance) ||
                string.IsNullOrWhiteSpace(requirement.RequirementText) || requirement.RequirementText.Length > 2000)
                throw new ArgumentException("Invalid requirement.");
            JobRequirementPolicy.Validate(requirement);
        }
        var reviewed = JobPostingImporter.ProposeProvidedText(proposal.Posting.Id, review.Employer, review.Title,
            review.Text, review.SourceUrl, DateTimeOffset.UtcNow);
        var job = JobPostingImporter.ConfirmReviewedRequirements(reviewed, review.Requirements, DateTimeOffset.UtcNow);
        var next = data with { Job = job, JobProposals = data.JobProposals.Where(p => JobRef(p.Posting) != proposalRef).ToList() };
        await SaveAsync(revision, next);
        return View(revision + 1, next);
    });

    public Task<WorkspaceApplicationPanel> DiscardProposalAsync(Guid proposalRef, long expectedRevision) =>
        WithWorkspace(async (revision, data) =>
        {
            CheckRevision(expectedRevision, revision);
            var profileMatches = data.ProfileProposals.Count(proposal => proposal.Id == proposalRef);
            var jobMatches = data.JobProposals.Count(proposal => JobRef(proposal.Posting) == proposalRef);
            if (profileMatches + jobMatches == 0) throw new PolicyException("ProposalNotFound");
            if (profileMatches + jobMatches != 1) throw new PolicyException("ProposalReferenceAmbiguous");
            var next = data with
            {
                ProfileProposals = data.ProfileProposals.Where(proposal => proposal.Id != proposalRef).ToList(),
                JobProposals = data.JobProposals.Where(proposal => JobRef(proposal.Posting) != proposalRef).ToList()
            };
            await SaveAsync(revision, next);
            return Panel(revision + 1, next);
        });

    private static void ValidateRoleGroup(string roleGroupId)
    {
        if (roleGroupId is null || roleGroupId.Length > 64 || roleGroupId.Length != 0 &&
            !Regex.IsMatch(roleGroupId, "^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Use a stable lowercase role-group id, or leave it empty.");
    }
    private static void RequireReviewedSources(WorkspaceData data)
    {
        if (data.Document is null || data.ResumeRef == Guid.Empty || data.Profile.VerifiedAt is null)
            throw new PolicyException("ProfileReviewRequired");
        if (data.Job?.RequirementsReviewedAt is null) throw new PolicyException("JobReviewRequired");
    }
    private static WorkspaceApplicationView ApplicationView(long revision, WorkspaceApplication application, WorkspaceData data) =>
        new(revision, application.Draft, QuestionResults(application, data), application.ReviewHash is not null,
            ReferencesCurrent(application, data), application.Proposals ?? [])
        { RoleGroupId = application.Job.RoleGroupId };

    private static WorkspaceApplicationPanel Panel(long revision, WorkspaceData data) => new(revision, References(revision, data),
        data.Applications.Select(application => ApplicationView(revision, Refresh(application, data), data)).ToArray(),
        data.ProfileProposals.Select(proposal => new WorkspaceProfileProposalView(proposal.Id, proposal.Request,
            proposal.Request.BaseVersion == data.Profile.Version ? "Pending" : "Conflict")).ToArray(),
        data.JobProposals);
}
