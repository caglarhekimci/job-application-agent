using System.Net.Mail;
using System.Text.Json;
using JobAgent.Core.Answers;
using JobAgent.Core.Jobs;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Documents;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace JobAgent.Infrastructure.Workspace;

public sealed record ReviewedExperience
{
    public string SourceSpan { get; init; } = "";
    public DateOnly Start { get; init; }
    public DateOnly? End { get; init; }
    public string Role { get; init; } = "";
    public ExperienceKind Kind { get; init; }
    public List<string> Skills { get; init; } = [];
}

public sealed record ProfileReview
{
    public long ExpectedRevision { get; init; }
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public decimal? SalaryTarget { get; init; }
    public decimal? SalaryPrivateMinimum { get; init; }
    public List<ReviewedExperience> Experience { get; init; } = [];
}

public sealed record JobReview
{
    public long ExpectedRevision { get; init; }
    public string Employer { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public List<JobRequirement> Requirements { get; init; } = [];
}

public sealed record WorkspaceView(long Revision, CandidateProfile Profile,
    ResumeImportResult? Document, string? FileName, JobPosting? Job, JobEvaluation? Evaluation,
    decimal? SalaryPrivateMinimum);

public sealed record WorkspaceAnswerReview
{
    public long ExpectedRevision { get; init; }
    public string SemanticKey { get; init; } = "";
    public string Answer { get; init; } = "";
    public AnswerScopeType Scope { get; init; }
    public string Language { get; init; } = "tr";
    public List<string> EvidenceIds { get; init; } = [];
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record WorkspaceAnswerRevocation
{
    public long ExpectedRevision { get; init; }
    public AnswerMemoryKey Key { get; init; } = new();
}

internal sealed record WorkspaceData
{
    public CandidateProfile Profile { get; init; } = new();
    public List<CandidateProfile> PreviousVersions { get; init; } = [];
    public ResumeImportResult? Document { get; init; }
    public string? FileName { get; init; }
    public byte[] ResumeBytes { get; init; } = [];
    public JobPosting? Job { get; init; }
    public bool PrivateMinimumProvided { get; init; }
}

// This service is available only to the authenticated local UI. It has no network adapter.
// One protected SQLite payload keeps the document, current profile and revision history atomic.
public sealed class LocalWorkspace : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string connectionString;
    private readonly IPayloadProtector protector;
    private readonly ResumeImporter importer;
    private readonly SemaphoreSlim gate = new(1, 1);

    public LocalWorkspace(string dataDirectory, string checkoutRoot, IPayloadProtector protector,
        ResumeImporter? importer = null)
    {
        var directory = Path.GetFullPath(dataDirectory);
        var checkout = Path.GetFullPath(checkoutRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (directory.Equals(checkout, StringComparison.OrdinalIgnoreCase) ||
            directory.StartsWith(checkout + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Workspace data must be outside the checkout.");
        if (protector.IsPlaintext) throw new InvalidOperationException("Personal workspace requires protected storage.");
        this.protector = protector;
        this.importer = importer ?? new();
        Directory.CreateDirectory(directory);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "workspace.db"),
            Pooling = false
        }.ToString();
    }

    public async Task<WorkspaceView> GetAsync()
    {
        await gate.WaitAsync();
        try { var (revision, data) = await ReadAsync(); return View(revision, data); }
        finally { gate.Release(); }
    }

    public async Task<WorkspaceView> ImportAsync(Stream input, string fileName, CancellationToken cancellationToken = default)
    {
        using var bytes = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (bytes.Length + read > ResumeImporter.MaximumBytes) throw new ArgumentException("Resume exceeds 2 MiB.");
            await bytes.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        bytes.Position = 0;
        var document = await importer.ImportAsync(bytes, fileName, cancellationToken);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var (revision, data) = await ReadAsync();
            var profile = data.Profile with
            {
                Version = data.Profile.Version + 1,
                VerifiedAt = null,
                Facts = document.ProposedFacts,
                Experience = [],
                Answers = [],
                LocalConfirmations = [],
                Consents = [],
                Salary = data.Profile.Salary with { ConfirmedAt = null }
            };
            var next = data with
            {
                Profile = profile,
                Document = document,
                FileName = fileName,
                ResumeBytes = bytes.ToArray(),
                PreviousVersions = [.. data.PreviousVersions, data.Profile]
            };
            await SaveAsync(revision, next);
            return View(revision + 1, next);
        }
        finally { gate.Release(); }
    }

    public async Task<WorkspaceView> ReviewProfileAsync(ProfileReview review)
    {
        await gate.WaitAsync();
        try
        {
            var (revision, data) = await ReadAsync();
            CheckRevision(review.ExpectedRevision, revision);
            if (data.Document is null || data.Document.Status != ResumeImportStatus.ReadyForReview)
                throw new InvalidOperationException("Import a readable resume first.");
            if (string.IsNullOrWhiteSpace(review.FullName) || review.FullName.Length > 200 ||
                !MailAddress.TryCreate(review.Email, out var email) || email.Address != review.Email || review.Email.Length > 254)
                throw new ArgumentException("Name and email must be reviewed.");
            if (review.SalaryTarget is < 0 or > 1_000_000_000 || review.SalaryPrivateMinimum is < 0 or > 1_000_000_000 ||
                review.SalaryPrivateMinimum > review.SalaryTarget || review.Experience.Count > 50)
                throw new ArgumentException("Invalid salary or experience.");
            var now = DateTimeOffset.UtcNow;
            var facts = new List<EvidenceFact>();
            var periods = new List<ExperiencePeriod>();
            foreach (var item in review.Experience)
            {
                var source = data.Document.EvidenceSegments.FirstOrDefault(s => s.SourceSpan == item.SourceSpan);
                if (source is null || item.Start == default || item.Start > DateOnly.FromDateTime(now.UtcDateTime) ||
                    item.End < item.Start || item.End > DateOnly.FromDateTime(now.UtcDateTime) ||
                    string.IsNullOrWhiteSpace(item.Role) || item.Role.Length > 200 || !Enum.IsDefined(item.Kind) ||
                    item.Skills.Count is 0 or > 30 || item.Skills.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 80))
                    throw new ArgumentException("Experience needs valid dates and a selected document source.");
                var factId = Guid.NewGuid().ToString("N");
                facts.Add(new()
                {
                    Id = factId,
                    Kind = item.Kind.ToString(),
                    Value = source.Text,
                    SourceDocumentId = data.Document.DocumentId,
                    SourceSpan = source.SourceSpan,
                    VerificationStatus = VerificationStatus.Verified,
                    ValidFrom = now
                });
                periods.Add(new()
                {
                    Start = item.Start,
                    End = item.End,
                    Role = item.Role.Trim(),
                    Kind = item.Kind,
                    Skills = item.Skills.Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    EvidenceIds = [factId]
                });
            }
            var salary = new SalaryPreference
            {
                Target = new() { Amount = review.SalaryTarget ?? 0, Currency = "TRY", Period = SalaryPeriod.Month, TaxBasis = TaxBasis.Net },
                PrivateMinimum = new() { Amount = review.SalaryPrivateMinimum ?? 0, Currency = "TRY", Period = SalaryPeriod.Month, TaxBasis = TaxBasis.Net },
                ConfirmedAt = review.SalaryTarget is null ? null : now
            };
            var profile = data.Profile with
            {
                Version = data.Profile.Version + 1,
                FullName = review.FullName.Trim(),
                Email = review.Email,
                Locale = "tr",
                Facts = facts,
                Experience = periods,
                Salary = salary,
                VerifiedAt = now,
                LocalConfirmations = [],
                Answers = []
            };
            profile = ProfilePolicy.ConfirmLocally(profile, now, ProfileField.FullName, ProfileField.Email);
            if (review.SalaryTarget is not null) profile = ProfilePolicy.ConfirmLocally(profile, now, ProfileField.Salary);
            var next = data with
            {
                Profile = profile,
                PreviousVersions = [.. data.PreviousVersions, data.Profile],
                PrivateMinimumProvided = review.SalaryPrivateMinimum is not null
            };
            await SaveAsync(revision, next);
            return View(revision + 1, next);
        }
        finally { gate.Release(); }
    }

    public async Task<WorkspaceView> ReviewJobAsync(JobReview review)
    {
        if (string.IsNullOrWhiteSpace(review.Employer) || string.IsNullOrWhiteSpace(review.Title) ||
            review.Employer.Length > 200 || review.Title.Length > 300 || review.Text.Length > 100_000 ||
            review.Requirements.Count > 100)
            throw new ArgumentException("Invalid posting.");
        foreach (var requirement in review.Requirements)
            if (string.IsNullOrWhiteSpace(requirement.RequirementText) || requirement.RequirementText.Length > 2000 ||
                !Enum.IsDefined(requirement.Type) || !Enum.IsDefined(requirement.Importance) ||
                (requirement.Type == RequirementType.ProfessionalExperienceYears &&
                 (string.IsNullOrWhiteSpace(requirement.Skill) || requirement.MinimumYears is null or < 0 or > 80)))
                throw new ArgumentException("Review each requirement before evaluating.");
        var proposal = JobPostingImporter.ProposeProvidedText(Guid.NewGuid().ToString("N"), review.Employer,
            review.Title, review.Text, review.SourceUrl, DateTimeOffset.UtcNow);
        var job = JobPostingImporter.ConfirmReviewedRequirements(proposal, review.Requirements, DateTimeOffset.UtcNow);
        await gate.WaitAsync();
        try
        {
            var (revision, data) = await ReadAsync();
            CheckRevision(review.ExpectedRevision, revision);
            if (data.Job is { } previous && previous.Employer.Equals(job.Employer, StringComparison.OrdinalIgnoreCase) &&
                ((!string.IsNullOrEmpty(job.CanonicalUrl) && previous.CanonicalUrl == job.CanonicalUrl) ||
                 (string.IsNullOrEmpty(job.CanonicalUrl) && string.IsNullOrEmpty(previous.CanonicalUrl) &&
                  previous.Title == job.Title && previous.Text == job.Text)))
                job = job with { Id = previous.Id, ExternalId = previous.ExternalId, FirstSeenAt = previous.FirstSeenAt };
            var next = data with { Job = job };
            await SaveAsync(revision, next);
            return View(revision + 1, next);
        }
        finally { gate.Release(); }
    }

    public async Task<AnswerResolution> ResolveAsync(FormQuestion question)
    {
        if (question.Key.Length > 200 || question.Label.Length > 2000) throw new ArgumentException("Question is too long.");
        var state = await GetAsync();
        return AnswerResolver.Resolve(question, state.Profile, state.Job ?? new(), DateTimeOffset.UtcNow);
    }

    public async Task<WorkspaceView> ReviewAnswerAsync(WorkspaceAnswerReview review)
    {
        await gate.WaitAsync();
        try
        {
            var (revision, data) = await ReadAsync();
            CheckRevision(review.ExpectedRevision, revision);
            if (data.Profile.VerifiedAt is null) throw new InvalidOperationException("Review the profile first.");
            if (review.Scope != AnswerScopeType.Default && data.Job is null)
                throw new InvalidOperationException("Scoped memory requires a reviewed job.");
            if (data.Profile.Answers.Count >= 200)
                throw new InvalidOperationException("Remove an answer before adding more than 200.");
            var profile = AnswerMemoryService.UpsertReviewed(data.Profile, new()
            {
                SemanticKey = review.SemanticKey,
                Answer = review.Answer,
                Scope = review.Scope,
                ScopeId = review.Scope switch
                {
                    AnswerScopeType.Default => null,
                    AnswerScopeType.Company => data.Job!.Employer,
                    AnswerScopeType.Application => data.Job!.Id,
                    _ => throw new ArgumentException("Invalid answer scope.")
                },
                Language = review.Language,
                EvidenceIds = review.EvidenceIds,
                ExpiresAt = review.ExpiresAt
            }, DateTimeOffset.UtcNow);
            var next = data with { Profile = profile, PreviousVersions = [.. data.PreviousVersions, data.Profile] };
            await SaveAsync(revision, next);
            return View(revision + 1, next);
        }
        finally { gate.Release(); }
    }

    public async Task<WorkspaceView> RevokeAnswerAsync(WorkspaceAnswerRevocation request)
    {
        await gate.WaitAsync();
        try
        {
            var (revision, data) = await ReadAsync();
            CheckRevision(request.ExpectedRevision, revision);
            var profile = AnswerMemoryService.Revoke(data.Profile, request.Key);
            if (profile.Version == data.Profile.Version) return View(revision, data);
            var next = data with { Profile = profile, PreviousVersions = [.. data.PreviousVersions, data.Profile] };
            await SaveAsync(revision, next);
            return View(revision + 1, next);
        }
        finally { gate.Release(); }
    }

    public async Task<string> ExportAsync()
    {
        await gate.WaitAsync();
        try { var (_, data) = await ReadAsync(); return JsonSerializer.Serialize(data, Json); }
        finally { gate.Release(); }
    }

    public async Task DeleteAsync(long expectedRevision)
    {
        await gate.WaitAsync();
        try
        {
            var (revision, _) = await ReadAsync();
            CheckRevision(expectedRevision, revision);
            await SaveAsync(revision, new());
        }
        finally { gate.Release(); }
    }

    private static WorkspaceView View(long revision, WorkspaceData data) => new(revision, data.Profile,
        data.Document, data.FileName, data.Job, data.Job is null ? null : data.Job.Requirements.Count == 0
            ? new() { Status = JobEvaluationStatus.InsufficientInformation }
            : JobEvaluator.Evaluate(data.Job, data.Profile, DateTimeOffset.UtcNow),
        data.PrivateMinimumProvided ? data.Profile.Salary.PrivateMinimum.Amount : null);

    private async Task<SqliteConnection> ConnectAsync()
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA secure_delete=ON; CREATE TABLE IF NOT EXISTS Workspace (Id INTEGER PRIMARY KEY CHECK(Id=1), Revision INTEGER NOT NULL, Payload BLOB NOT NULL);";
        await command.ExecuteNonQueryAsync();
        return connection;
    }

    private async Task<(long Revision, WorkspaceData Data)> ReadAsync()
    {
        await using var connection = await ConnectAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Revision, Payload FROM Workspace WHERE Id=1";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (0, new());
        return (reader.GetInt64(0), JsonSerializer.Deserialize<WorkspaceData>(protector.Unprotect((byte[])reader[1]), Json)
            ?? throw new InvalidDataException("Invalid protected workspace."));
    }

    private async Task SaveAsync(long expectedRevision, WorkspaceData data)
    {
        if (data.PreviousVersions.Count > 100) throw new InvalidOperationException("Export and clear the workspace before exceeding 100 profile versions.");
        var payload = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(data, Json));
        await using var connection = await ConnectAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = expectedRevision == 0
            ? "INSERT OR IGNORE INTO Workspace VALUES(1, 1, $payload)"
            : "UPDATE Workspace SET Revision=Revision+1, Payload=$payload WHERE Id=1 AND Revision=$revision";
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$revision", expectedRevision);
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("Workspace changed; refresh before confirming.");
    }

    private static void CheckRevision(long expected, long actual)
    {
        if (expected != actual) throw new InvalidOperationException("Workspace changed; refresh before confirming.");
    }

    public void Dispose() => gate.Dispose();
}
