using JobAgent.Core;
using JobAgent.Core.Applications;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace JobAgent.Mcp.Tests;

internal sealed class RuntimeFixture : IAsyncDisposable
{
    public RuntimeFixture()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "job-agent-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }
    public string ProfileDatabasePath => Path.Combine(DirectoryPath, "profiles.db");
    public string ApplicationDatabasePath => Path.Combine(DirectoryPath, "synthetic-applications.db");
    public CandidateProfile Profile { get; private set; } = SyntheticData.Profile();
    public WorkflowRecord Application { get; private set; } = default!;

    public async Task SeedAsync()
    {
        var now = new DateTimeOffset(2026, 9, 18, 0, 0, 0, TimeSpan.Zero);
        Profile = ProfilePolicy.ConfirmLocally(SyntheticData.Profile(), now,
            ProfileField.FullName, ProfileField.Email, ProfileField.Salary);
        var profileRepository = new ProfileRepository(new()
        {
            DatabasePath = ProfileDatabasePath,
            CheckoutRoot = Environment.CurrentDirectory
        }, new WindowsDpapiPayloadProtector());
        await profileRepository.InitializeAsync();
        await profileRepository.SaveInitialAsync(Profile);

        var draft = new ApplicationDraft
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ProfileId = Profile.Id,
            ProfileVersion = Profile.Version,
            JobKey = "synthetic-job-1",
            JobTitle = "C# Engineer",
            Employer = "Synthetic Employer",
            RecipientOrigin = "http://127.0.0.1:5179",
            ResumeRef = "synthetic-resume.txt",
            ResumeHash = "SYNTHETIC-HASH",
            Status = ApplicationStatus.SubmittedVerified,
            Synthetic = true
        };
        Application = new(draft, Evidence: new(
            "receipt-synthetic-1", "application-synthetic-1", "SYNTHETIC-HASH", now));
        var journal = new ApplicationJournal(ApplicationDatabasePath);
        await journal.InitializeAsync();
        await journal.CreateAsync(Application);
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        await Task.Yield();
        if (Directory.Exists(DirectoryPath))
            Directory.Delete(DirectoryPath, recursive: true);
    }
}
