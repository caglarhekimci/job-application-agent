using System.Text;
using JobAgent.Core;
using JobAgent.Core.Answers;
using JobAgent.Core.Applications;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Browser;
using JobAgent.Infrastructure.Documents;
using JobAgent.Infrastructure.Storage;

namespace JobAgent.Infrastructure.Applications;

public sealed class DemoWorkflow : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ApplicationJournal journal;
    private readonly ProfileRepository profiles;
    private readonly string origin;
    private readonly ResumeDocument resume;
    private CandidateProfile? profile;
    private CandidateProfile? pendingProfile;
    private string? resumeText;
    private bool initialized;
    private Guid? currentId;
    private ManagedBrowserSession? browser;
    private CancellationTokenSource operation = new();
    private string? approvalSession;

    public DemoWorkflow(string directory, string careerOrigin, string checkoutRoot)
    {
        origin = careerOrigin.TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "http" || uri.Host != "127.0.0.1")
            throw new PolicyException("SyntheticOriginRequired");
        journal = new(Path.Combine(directory, "synthetic-applications.db"));
        profiles = new(new ProfileStoreOptions
        {
            DatabasePath = Path.Combine(directory, "profiles.db"),
            CheckoutRoot = checkoutRoot,
            AllowSyntheticPlaintextForLinuxTests = !OperatingSystem.IsWindows()
        }, OperatingSystem.IsWindows() ? new WindowsDpapiPayloadProtector() : new SyntheticPlaintextPayloadProtector());
        using var input = typeof(DemoWorkflow).Assembly.GetManifestResourceStream("JobAgent.SyntheticResume.txt")
            ?? throw new InvalidOperationException("Synthetic resume resource missing.");
        using var buffer = new MemoryStream(); input.CopyTo(buffer);
        resume = new("synthetic-resume", "synthetic-resume.txt", buffer.ToArray());
    }

    private JobPosting Job => SyntheticData.Job() with
    {
        Id = "synthetic-dotnet",
        Employer = "Synthetic Labs",
        Title = ".NET Developer",
        SourceUrl = origin + "/jobs/synthetic-dotnet",
        Text = "Sentetik ilan: en az 2 yıl profesyonel C# deneyimi. Uzaktan çalışma.",
        Requirements = [new() { Id = "csharp", RequirementText = "En az 2 yıl profesyonel C#",
            Type = RequirementType.ProfessionalExperienceYears, Importance = RequirementImportance.Mandatory,
            Skill = "C#", MinimumYears = 2 }]
    };
    private static readonly FormQuestion[] Questions =
    [
        new() { Key = "contact.name", Label = "Full name", Language = "en" },
        new() { Key = "contact.email", Label = "Email", Language = "en" },
        new() { Key = "salary.expected.monthly.net.TRY", Label = "Expected monthly net salary (TRY)", Language = "en" },
        new() { Key = "experience.professional.csharp.years", Label = "Professional C# years", Language = "en" }
    ];

    private async Task Initialize()
    {
        if (initialized) return;
        await journal.InitializeAsync(); await profiles.InitializeAsync();
        await journal.RecoverInterruptedAsync();
        profile = await profiles.GetLatestAsync(SyntheticData.Profile().Id);
        currentId = (await journal.ListAsync()).LastOrDefault()?.Draft.Id;
        initialized = true;
    }

    public async Task<object> GetStateAsync()
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            var shown = profile ?? pendingProfile;
            var run = currentId is { } id ? await journal.GetAsync(id) : null;
            return new
            {
                mode = "Fixture",
                profileConfirmed = profile is not null,
                profile = shown is null ? null : new
                {
                    shown.Id,
                    shown.Version,
                    shown.FullName,
                    shown.Email,
                    shown.Facts,
                    shown.Experience,
                    salary = shown.Salary.Target
                },
                resumeText,
                resumeHash = resume.Hash,
                job = Job,
                evaluation = profile is null ? null : JobEvaluator.Evaluate(Job, profile, DateTimeOffset.UtcNow),
                answers = profile is null ? null : Questions.ToDictionary(q => q.Key,
                    q => AnswerResolver.Resolve(q, profile, Job, DateTimeOffset.UtcNow)),
                application = run is null ? null : new
                {
                    draft = run.Draft,
                    evidence = run.Evidence,
                    error = run.Error,
                    hostReviewRequested = run.HostReviewRequestedAt is not null,
                    submissionApproved = HasValidSubmissionApproval(run, DateTimeOffset.UtcNow)
                }
            };
        }
        finally { gate.Release(); }
    }

    public async Task LoadFixtureAsync()
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            using var stream = new MemoryStream(resume.Bytes, false);
            var imported = await new ResumeImporter().ImportTextAsync(stream, resume.FileName);
            resumeText = imported.Text;
            pendingProfile = SyntheticData.Profile() with
            {
                VerifiedAt = null,
                LocalConfirmations = [],
                Facts = SyntheticData.Profile().Facts.Select(f => f with
                { VerificationStatus = VerificationStatus.Proposed, SourceDocumentId = imported.DocumentId, SourceSpan = "line:4" }).ToList()
            };
        }
        finally { gate.Release(); }
    }

    // Only the authenticated UI route calls these approval methods. They are not MCP tools.
    public async Task ConfirmProfileFromUiAsync()
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            if (profile is not null) return;
            var pending = pendingProfile ?? throw new PolicyException("ImportRequired");
            var now = DateTimeOffset.UtcNow;
            var confirmed = ProfilePolicy.ConfirmLocally(pending with
            {
                VerifiedAt = now,
                Facts = pending.Facts.Select(f => f with { VerificationStatus = VerificationStatus.Verified }).ToList(),
                Salary = pending.Salary with { ConfirmedAt = now }
            }, now, ProfileField.FullName, ProfileField.Email, ProfileField.Salary);
            await profiles.SaveInitialAsync(confirmed);
            profile = confirmed;
        }
        finally { gate.Release(); }
    }

    public async Task CreateDraftAsync()
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            await CreateDraftCoreAsync();
        }
        finally { gate.Release(); }
    }

    public async Task<HostApplicationSummary> CreateSyntheticDraftForHostAsync()
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            return ToHostSummary(await CreateDraftCoreAsync());
        }
        finally { gate.Release(); }
    }

    public async Task<HostApplicationSummary> RequestHostReviewAsync(Guid applicationRef)
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            var run = await Current(applicationRef);
            if (run.Draft.Status is not (ApplicationStatus.ReadyForDataSharing or ApplicationStatus.AwaitingSubmissionApproval))
                throw new PolicyException("ApplicationNotActionable");
            if (run.HostReviewRequestedAt is null)
            {
                await journal.UpdateAsync(run.Draft.Id, run.Draft.Status,
                    r => r with { HostReviewRequestedAt = DateTimeOffset.UtcNow });
                run = await Current(applicationRef);
            }
            return ToHostSummary(run);
        }
        finally { gate.Release(); }
    }

    public async Task<HostApplicationSummary> GetHostStatusAsync(Guid applicationRef)
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            return ToHostSummary(await Current(applicationRef));
        }
        finally { gate.Release(); }
    }

    public async Task ShareAndFillFromUiAsync(string sessionId)
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            var run = await Current();
            if (run.Draft.Status != ApplicationStatus.ReadyForDataSharing) throw new PolicyException("InvalidStateTransition");
            var approval = ApprovalPolicy.GrantFromUserInterface(run.Draft, ApprovalPurpose.ShareData, sessionId, DateTimeOffset.UtcNow);
            await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.ReadyForDataSharing,
                r => r with
                {
                    Sharing = ApprovalPolicy.Consume(r.Draft, approval, ApprovalPurpose.ShareData, DateTimeOffset.UtcNow),
                    Submission = null,
                    Error = null,
                    Draft = r.Draft with { Status = ApplicationStatus.Filling }
                });
            approvalSession = sessionId;
            operation.Dispose(); operation = new();
            browser = new(new Uri(origin));
            try
            {
                await browser.PrepareAsync(run.Draft, approval, resume, operation.Token);
                await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.Filling,
                    r => r with { Draft = r.Draft with { Status = ApplicationStatus.AwaitingSubmissionApproval } });
            }
            catch (Exception e) when (e is PolicyException or Microsoft.Playwright.PlaywrightException or OperationCanceledException)
            {
                var manualTakeover = e is PolicyException { Code: "ManualTakeoverRequired" };
                await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.Filling,
                    r => r with
                    {
                        Draft = r.Draft with
                        {
                            Status = operation.IsCancellationRequested ? ApplicationStatus.Cancelled
                            : manualTakeover ? ApplicationStatus.NeedsInput : ApplicationStatus.FailedBeforeSubmission
                        },
                        Error = e is PolicyException p ? p.Code : "BrowserPreparationFailed"
                    });
                await browser.DisposeAsync(); browser = null;
            }
        }
        finally { gate.Release(); }
    }

    public async Task SubmitFromUiAsync(string sessionId)
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            var run = await Current();
            try { await EnsureUiCanApproveAsync(run, sessionId); }
            catch (PolicyException e) when (e.Code == "ManualTakeoverRequired") { return; }
            var approval = ApprovalPolicy.GrantFromUserInterface(run.Draft, ApprovalPurpose.Submit, sessionId, DateTimeOffset.UtcNow);
            await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.AwaitingSubmissionApproval,
                r => r with { Submission = approval });
            await ExecuteStoredApprovalCoreAsync(run.Draft.Id);
        }
        finally { gate.Release(); }
    }

    // Approval remains a trusted local UI operation. Host callers can only execute a receipt already stored here.
    public async Task<HostApplicationSummary> ApproveForHostFromUiAsync(Guid applicationRef, string sessionId)
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            var run = await Current(applicationRef);
            if (run.HostReviewRequestedAt is null) throw new PolicyException("HostReviewNotRequested");
            try { await EnsureUiCanApproveAsync(run, sessionId); }
            catch (PolicyException e) when (e.Code == "ManualTakeoverRequired")
            { return ToHostSummary(await Current(applicationRef)); }
            var approval = ApprovalPolicy.GrantFromUserInterface(run.Draft, ApprovalPurpose.Submit, sessionId, DateTimeOffset.UtcNow);
            await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.AwaitingSubmissionApproval,
                r => r with { Submission = approval, Error = null });
            return ToHostSummary(await Current(applicationRef));
        }
        finally { gate.Release(); }
    }

    public async Task<HostApplicationSummary> ExecuteAlreadyApprovedAsync(Guid applicationRef)
    {
        await gate.WaitAsync();
        try
        {
            await Initialize();
            await Current(applicationRef);
            return await ExecuteStoredApprovalCoreAsync(applicationRef);
        }
        finally { gate.Release(); }
    }

    public async Task CancelAsync()
    {
        await operation.CancelAsync();
        await gate.WaitAsync();
        try
        {
            var run = await Current();
            if (run.Draft.Status is ApplicationStatus.Submitting or ApplicationStatus.SubmittedVerified or ApplicationStatus.SubmittedUnverified)
                throw new PolicyException("AlreadySubmittedCannotWithdraw");
            await journal.UpdateAsync(run.Draft.Id, run.Draft.Status,
                r => r with { Draft = r.Draft with { Status = ApplicationStatus.Cancelled }, Error = "CancelledByUser" });
            if (browser is not null) { await browser.DisposeAsync(); browser = null; }
        }
        finally { gate.Release(); }
    }

    private async Task<WorkflowRecord> CreateDraftCoreAsync()
    {
        if (currentId is { } existing) return await Current(existing);
        var verified = profile ?? throw new PolicyException("ProfileReviewRequired");
        var answers = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var question in Questions)
        {
            var answer = AnswerResolver.Resolve(question, verified, Job, DateTimeOffset.UtcNow);
            if (answer.Status != AnswerStatus.Resolved || answer.Value is null) throw new PolicyException("NeedsInput");
            answers.Add(question.Key, answer.Value);
        }
        var draft = new ApplicationDraft
        {
            ProfileId = verified.Id,
            ProfileVersion = verified.Version,
            Synthetic = true,
            JobKey = Job.Id,
            JobTitle = Job.Title,
            Employer = Job.Employer,
            RecipientOrigin = origin,
            ResumeRef = resume.Reference,
            ResumeHash = resume.Hash,
            Answers = answers
        };
        await journal.CreateAsync(new(draft));
        currentId = draft.Id;
        return await Current(draft.Id);
    }

    private async Task EnsureUiCanApproveAsync(WorkflowRecord run, string sessionId)
    {
        if (run.Draft.Status != ApplicationStatus.AwaitingSubmissionApproval || browser is null)
            throw new PolicyException("SubmissionNotReady");
        if (approvalSession != sessionId) throw new PolicyException("UserSessionChanged");
        try { await browser.EnsureReadyForSubmissionAsync(run.Draft, operation.Token); }
        catch (PolicyException e) when (e.Code == "ManualTakeoverRequired")
        {
            await PauseForManualTakeoverAsync(run);
            throw;
        }
    }

    private async Task<HostApplicationSummary> ExecuteStoredApprovalCoreAsync(Guid applicationRef)
    {
        var run = await Current(applicationRef);
        if (run.Draft.Status is ApplicationStatus.SubmittedVerified or ApplicationStatus.SubmittedUnverified)
            return ToHostSummary(run);
        if (run.Draft.Status != ApplicationStatus.AwaitingSubmissionApproval || browser is null)
            throw new PolicyException("SubmissionNotReady");

        var approval = run.Submission;
        ApprovalPolicy.Validate(run.Draft, approval, ApprovalPurpose.Submit, DateTimeOffset.UtcNow);
        try { await browser.EnsureReadyForSubmissionAsync(run.Draft, operation.Token); }
        catch (PolicyException e) when (e.Code == "ManualTakeoverRequired")
        {
            await PauseForManualTakeoverAsync(run);
            return ToHostSummary(await Current(applicationRef));
        }

        if (!await journal.ClaimSubmissionAsync(run.Draft.Id, DateTimeOffset.UtcNow))
        {
            var changed = await Current(applicationRef);
            if (changed.Draft.Status is ApplicationStatus.SubmittedVerified or ApplicationStatus.SubmittedUnverified)
                return ToHostSummary(changed);
            throw new PolicyException("SubmissionAlreadyClaimed");
        }

        SubmissionEvidence? evidence = null;
        try { evidence = await browser.SubmitAsync(run.Draft, approval!, operation.Token); }
        catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        { /* A durable attempt exists. Never claim success or retry after uncertainty. */ }
        finally
        {
            try
            {
                await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.Submitting,
                    r => r with
                    {
                        Draft = r.Draft with { Status = evidence is null ? ApplicationStatus.SubmittedUnverified : ApplicationStatus.SubmittedVerified },
                        Evidence = evidence,
                        Error = evidence is null ? "SubmissionOutcomeUnknown" : null
                    });
            }
            finally { await browser.DisposeAsync(); browser = null; }
        }
        return ToHostSummary(await Current(applicationRef));
    }

    private async Task PauseForManualTakeoverAsync(WorkflowRecord run)
    {
        await journal.UpdateAsync(run.Draft.Id, ApplicationStatus.AwaitingSubmissionApproval,
            r => r with
            {
                Draft = r.Draft with { Status = ApplicationStatus.NeedsInput },
                Submission = null,
                Error = "ManualTakeoverRequired"
            });
        await browser!.DisposeAsync();
        browser = null;
    }

    private static HostApplicationSummary ToHostSummary(WorkflowRecord run) => new(
        run.Draft.Id,
        run.Draft.Status,
        run.HostReviewRequestedAt is not null,
        HasValidSubmissionApproval(run, DateTimeOffset.UtcNow),
        run.Evidence?.ReceiptId);

    private static bool HasValidSubmissionApproval(WorkflowRecord run, DateTimeOffset now)
    {
        try
        {
            ApprovalPolicy.Validate(run.Draft, run.Submission, ApprovalPurpose.Submit, now);
            return true;
        }
        catch (PolicyException) { return false; }
    }

    private Task<WorkflowRecord> Current() => currentId is { } id
        ? Current(id) : throw new PolicyException("ApplicationNotFound");

    private async Task<WorkflowRecord> Current(Guid applicationRef)
    {
        if (currentId is not { } id || id != applicationRef) throw new PolicyException("ApplicationNotFound");
        return await journal.GetAsync(id) ?? throw new PolicyException("ApplicationNotFound");
    }

    public async ValueTask DisposeAsync()
    {
        await operation.CancelAsync();
        if (browser is not null) await browser.DisposeAsync();
        operation.Dispose(); gate.Dispose();
    }
}
