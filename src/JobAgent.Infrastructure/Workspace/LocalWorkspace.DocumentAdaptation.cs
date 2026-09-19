using System.Text;
using JobAgent.Core.Applications;
using JobAgent.Core.Documents;

namespace JobAgent.Infrastructure.Workspace;

public enum WorkspaceDocumentAdaptationStatus { None, Proposed, Approved, Stale }

public sealed record WorkspaceDocumentAdaptationProposal(Guid ApplicationRef, long ExpectedRevision,
    string ExpectedApplicationPayloadHash);

public sealed record WorkspaceDocumentAdaptationApproval(Guid ApplicationRef, long ExpectedRevision,
    string ExpectedBundleHash);

public sealed record WorkspaceDocumentAdaptationView(long Revision, Guid ApplicationRef,
    WorkspaceDocumentAdaptationStatus Status, string OriginalText, string OriginalResumeHash,
    DocumentAdaptationProposal? Proposal, DateTimeOffset? ApprovedAt);

public sealed record WorkspaceDocumentExport(string FileName, string ContentHash, byte[] Bytes);

internal sealed record StoredDocumentAdaptation(Guid ApplicationRef, DocumentAdaptationProposal Proposal,
    string OriginalText,
    string? ApprovedBundleHash = null, DateTimeOffset? ApprovedAt = null);

internal sealed partial record WorkspaceData
{
    public List<StoredDocumentAdaptation> DocumentAdaptations { get; init; } = [];
}

// Extractive document proposals are local UI artifacts. They never replace the application resume reference.
public sealed partial class LocalWorkspace
{
    public Task<WorkspaceDocumentAdaptationView> GetDocumentAdaptationAsync(Guid applicationRef) =>
        WithWorkspace(async (revision, data) =>
        {
            await Task.CompletedTask;
            var application = FindApplication(data, applicationRef);
            return DocumentView(revision, application, data);
        });

    public Task<WorkspaceDocumentAdaptationView> ProposeDocumentAdaptationFromUiAsync(
        WorkspaceDocumentAdaptationProposal request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRevision(request.ExpectedRevision, revision);
        RequireReviewedSources(data);
        var application = Refresh(FindApplication(data, request.ApplicationRef), data);
        RequireEditable(application);
        if (!ReferencesCurrent(application, data)) throw new PolicyException("ApplicationSourcesChanged");
        if (!string.Equals(request.ExpectedApplicationPayloadHash, application.Draft.PayloadHash(),
                StringComparison.Ordinal))
            throw new PolicyException("PackageChanged");
        var proposal = DocumentAdapter.Create(AdaptationInput(application, data), timeProvider.GetUtcNow());
        var stored = new StoredDocumentAdaptation(application.Draft.Id, proposal, data.Document!.Text);
        var next = ReplaceDocumentAdaptation(data, stored);
        await SaveAsync(revision, next, "DocumentAdaptationProposed");
        return DocumentView(revision + 1, application, next);
    });

    public Task<WorkspaceDocumentAdaptationView> ApproveDocumentAdaptationFromUiAsync(
        WorkspaceDocumentAdaptationApproval request) => WithWorkspace(async (revision, data) =>
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckRevision(request.ExpectedRevision, revision);
        var application = FindApplication(data, request.ApplicationRef);
        var stored = FindDocumentAdaptation(data, request.ApplicationRef);
        if (!string.Equals(stored.Proposal.BundleHash, request.ExpectedBundleHash, StringComparison.Ordinal))
            throw new PolicyException("PackageChanged");
        if (!IsCurrent(application, data, stored.Proposal))
            throw new PolicyException("DocumentAdaptationStale");
        var approved = stored with
        {
            ApprovedBundleHash = stored.Proposal.BundleHash,
            ApprovedAt = timeProvider.GetUtcNow()
        };
        var next = ReplaceDocumentAdaptation(data, approved);
        await SaveAsync(revision, next, "DocumentAdaptationApproved");
        return DocumentView(revision + 1, application, next);
    });

    public Task<WorkspaceDocumentExport> ExportApprovedDocumentAsync(Guid applicationRef,
        string expectedBundleHash, AdaptedDocumentKind kind) => WithWorkspace<WorkspaceDocumentExport>(async (_, data) =>
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Unknown document kind.", nameof(kind));
        var application = FindApplication(data, applicationRef);
        var stored = FindDocumentAdaptation(data, applicationRef);
        if (!string.Equals(stored.Proposal.BundleHash, expectedBundleHash, StringComparison.Ordinal))
            throw new PolicyException("PackageChanged");
        if (!IsCurrent(application, data, stored.Proposal))
            throw new PolicyException("DocumentAdaptationStale");
        if (!string.Equals(stored.ApprovedBundleHash, expectedBundleHash, StringComparison.Ordinal))
            throw new PolicyException("DocumentApprovalRequired");
        var artifact = kind == AdaptedDocumentKind.Resume
            ? stored.Proposal.Resume : stored.Proposal.CoverLetter;
        await Task.CompletedTask;
        return new(kind == AdaptedDocumentKind.Resume ? "adapted-cv.txt" : "cover-letter.txt",
            artifact.ContentHash, Encoding.UTF8.GetBytes(artifact.Content));
    });

    private WorkspaceDocumentAdaptationView DocumentView(long revision, WorkspaceApplication application,
        WorkspaceData data)
    {
        if (data.Document is null) throw new PolicyException("ProfileReviewRequired");
        var stored = data.DocumentAdaptations.SingleOrDefault(item => item.ApplicationRef == application.Draft.Id);
        if (stored is null)
            return new(revision, application.Draft.Id, WorkspaceDocumentAdaptationStatus.None,
                data.Document.Text, application.Draft.ResumeHash, null, null);
        var current = IsCurrent(application, data, stored.Proposal);
        var status = !current ? WorkspaceDocumentAdaptationStatus.Stale
            : string.Equals(stored.ApprovedBundleHash, stored.Proposal.BundleHash, StringComparison.Ordinal)
                ? WorkspaceDocumentAdaptationStatus.Approved
                : WorkspaceDocumentAdaptationStatus.Proposed;
        return new(revision, application.Draft.Id, status, stored.OriginalText,
            stored.Proposal.Binding.ResumeHash, stored.Proposal, stored.ApprovedAt);
    }

    private bool IsCurrent(WorkspaceApplication application, WorkspaceData data,
        DocumentAdaptationProposal proposal)
    {
        if (!ReferencesCurrent(application, data)) return false;
        try
        {
            var current = DocumentAdapter.Create(AdaptationInput(Refresh(application, data), data),
                timeProvider.GetUtcNow());
            return string.Equals(current.BindingHash, proposal.BindingHash, StringComparison.Ordinal) &&
                   string.Equals(current.BundleHash, proposal.BundleHash, StringComparison.Ordinal);
        }
        catch (DocumentAdaptationException)
        {
            return false;
        }
    }

    private static DocumentAdaptationInput AdaptationInput(WorkspaceApplication application, WorkspaceData data)
    {
        if (data.Document is null) throw new PolicyException("ProfileReviewRequired");
        return new()
        {
            ApplicationRef = application.Draft.Id,
            ApplicationPayloadHash = application.Draft.PayloadHash(),
            Profile = data.Profile,
            ResumeRef = application.ResumeRef,
            ResumeHash = application.Draft.ResumeHash,
            SourceDocumentId = data.Document.DocumentId,
            OriginalText = data.Document.Text,
            SourceSegments = data.Document.EvidenceSegments
                .Select(segment => new AdaptationSourceSegment(segment.SourceSpan, segment.Text)).ToArray(),
            JobRef = JobRef(application.Job),
            Job = application.Job
        };
    }

    private static StoredDocumentAdaptation FindDocumentAdaptation(WorkspaceData data, Guid applicationRef) =>
        data.DocumentAdaptations.SingleOrDefault(item => item.ApplicationRef == applicationRef)
        ?? throw new PolicyException("DocumentAdaptationNotFound");

    private static WorkspaceData ReplaceDocumentAdaptation(WorkspaceData data, StoredDocumentAdaptation adaptation) =>
        data with
        {
            DocumentAdaptations =
            [
                .. data.DocumentAdaptations.Where(item => item.ApplicationRef != adaptation.ApplicationRef),
                adaptation
            ]
        };
}
