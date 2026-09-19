using JobAgent.Core.Applications;

namespace JobAgent.Infrastructure.Browser;

public interface IBrowserSession : IAsyncDisposable
{
    Task PrepareAsync(ApplicationDraft draft, ApprovalReceipt? sharing, ResumeDocument resume,
        CancellationToken ct = default);
    Task EnsureReadyForSubmissionAsync(ApplicationDraft draft, CancellationToken ct = default);
    Task<SubmissionEvidence?> SubmitAsync(ApplicationDraft draft, ApprovalReceipt? approval,
        CancellationToken ct = default);
}

internal enum BrowserField
{
    ContactName,
    ContactEmail,
    Salary,
    ProfessionalYears,
    WorkMode,
    Travel,
    ContactMethod,
    ContactWindow,
    Resume
}

internal enum BrowserStep { Contact, Questions, Final }

internal abstract record BrowserAction
{
    internal sealed record Navigate : BrowserAction;
    internal sealed record ReadVisibleControls(BrowserStep Step) : BrowserAction;
    internal sealed record FillText(BrowserField Field) : BrowserAction;
    internal sealed record SelectOption(BrowserField Field) : BrowserAction;
    internal sealed record SetCheckbox(BrowserField Field) : BrowserAction;
    internal sealed record ChooseRadio(BrowserField Field) : BrowserAction;
    internal sealed record UploadApprovedResume : BrowserAction;
    internal sealed record Advance(BrowserStep From) : BrowserAction;
}
