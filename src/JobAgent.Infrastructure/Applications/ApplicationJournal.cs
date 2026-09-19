using JobAgent.Core.Applications;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace JobAgent.Infrastructure.Applications;

public sealed record WorkflowRecord(ApplicationDraft Draft, ApprovalReceipt? Sharing = null,
    ApprovalReceipt? Submission = null, SubmissionEvidence? Evidence = null, string? Error = null,
    DateTimeOffset? HostReviewRequestedAt = null);

public sealed class ApplicationJournal
{
    private readonly TimeProvider timeProvider;

    public ApplicationJournal(string databasePath, TimeProvider? timeProvider = null)
    {
        DatabasePath = databasePath;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string DatabasePath { get; }
    private JournalContext Open() => new(DatabasePath);
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(DatabasePath))!);
        await ApplicationJournalSchema.InitializeAsync(DatabasePath, CancellationToken.None);
    }
    public async Task CreateAsync(WorkflowRecord record)
    {
        // This first runtime supports synthetic data only. Never silently store a real CV as plaintext.
        if (!record.Draft.Synthetic) throw new PolicyException("SensitiveRuntimeNotEnabled");
        await using var db = Open();
        db.Applications.Add(new JournalRow
        {
            Id = record.Draft.Id,
            JobIdentity = record.Draft.ProfileId + ":" + record.Draft.JobKey,
            State = record.Draft.Status,
            Body = JsonSerializer.Serialize(record),
            Revision = 1
        });
        db.ApplicationAuditEntries.Add(Audit(record.Draft.Id, 0, 1, null,
            record.Draft.Status, "ApplicationCreated"));
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
        { throw new PolicyException("DuplicateApplication"); }
    }
    public async Task<WorkflowRecord?> GetAsync(Guid id)
    {
        await using var db = Open();
        var row = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return row is null ? null : Decode(row);
    }
    public async Task<IReadOnlyList<WorkflowRecord>> ListAsync()
    {
        await using var db = Open();
        return (await db.Applications.AsNoTracking().ToListAsync()).Select(Decode).ToArray();
    }
    internal async Task UpdateAsync(Guid id, ApplicationStatus expected, Func<WorkflowRecord, WorkflowRecord> update)
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var row = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new PolicyException("ApplicationNotFound");
        if (row.State != expected) throw new PolicyException("InvalidStateTransition");
        var next = update(Decode(row));
        if (!next.Draft.Synthetic || next.Draft.Id != id) throw new PolicyException("InvalidApplication");
        var json = JsonSerializer.Serialize(next);
        var changed = await db.Applications.Where(x => x.Id == id && x.Revision == row.Revision)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Body, json)
                .SetProperty(x => x.State, next.Draft.Status).SetProperty(x => x.Revision, row.Revision + 1));
        if (changed != 1) throw new PolicyException("ConcurrentChange");
        db.ApplicationAuditEntries.Add(Audit(id, row.Revision, row.Revision + 1,
            row.State, next.Draft.Status, "StateTransition"));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    public async Task<bool> ClaimSubmissionAsync(Guid id, DateTimeOffset now)
    {
        try
        {
            await UpdateAsync(id, ApplicationStatus.AwaitingSubmissionApproval, record =>
            {
                ApprovalPolicy.Validate(record.Draft, record.Submission, ApprovalPurpose.Submit, now);
                return record with
                {
                    Submission = ApprovalPolicy.Consume(record.Draft, record.Submission!, ApprovalPurpose.Submit, now),
                    Draft = record.Draft with { Status = ApplicationStatus.Submitting }
                };
            });
            return true;
        }
        catch (PolicyException) { return false; }
    }
    public async Task RecoverInterruptedAsync()
    {
        foreach (var record in await ListAsync())
        {
            var state = record.Draft.Status;
            if (state is not (ApplicationStatus.Submitting or ApplicationStatus.Filling or ApplicationStatus.AwaitingSubmissionApproval)) continue;
            await UpdateAsync(record.Draft.Id, state, current => current with
            {
                Draft = current.Draft with
                {
                    Status = state == ApplicationStatus.Submitting
                    ? ApplicationStatus.SubmittedUnverified : ApplicationStatus.ReadyForDataSharing
                },
                Submission = state == ApplicationStatus.Submitting ? current.Submission : null,
                Error = state == ApplicationStatus.Submitting ? "SubmissionOutcomeUnknown" : "BrowserSessionLost"
            });
        }
    }
    private static WorkflowRecord Decode(JournalRow row) => JsonSerializer.Deserialize<WorkflowRecord>(row.Body)
        ?? throw new InvalidDataException("Invalid application journal.");

    private ApplicationAuditRow Audit(Guid id, int previousRevision, int newRevision,
        ApplicationStatus? fromState, ApplicationStatus toState, string operation) => new()
        {
            ApplicationId = id,
            PreviousRevision = previousRevision,
            NewRevision = newRevision,
            FromState = fromState,
            ToState = toState,
            Operation = operation,
            Actor = "ApplicationRuntime",
            OccurredAt = timeProvider.GetUtcNow()
        };
}

internal sealed class JournalRow
{
    public Guid Id { get; set; }
    public string JobIdentity { get; set; } = "";
    public ApplicationStatus State { get; set; }
    public string Body { get; set; } = "";
    public int Revision { get; set; }
}

internal sealed class ApplicationAuditRow
{
    public long Id { get; set; }
    public Guid ApplicationId { get; set; }
    public int PreviousRevision { get; set; }
    public int NewRevision { get; set; }
    public ApplicationStatus? FromState { get; set; }
    public ApplicationStatus ToState { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

internal sealed class JournalContext(string path) : DbContext
{
    public DbSet<JournalRow> Applications => Set<JournalRow>();
    public DbSet<ApplicationAuditRow> ApplicationAuditEntries => Set<ApplicationAuditRow>();
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite(
        new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString());
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<JournalRow>().HasKey(x => x.Id);
        model.Entity<JournalRow>().HasIndex(x => x.JobIdentity).IsUnique();
        model.Entity<ApplicationAuditRow>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ApplicationId, x.NewRevision });
            entity.Property(x => x.Operation).IsRequired();
            entity.Property(x => x.Actor).IsRequired();
        });
    }
}
