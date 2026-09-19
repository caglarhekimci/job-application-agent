using JobAgent.Core;
using JobAgent.Core.Applications;
using JobAgent.Core.Profiles;
using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

if (args.Length != 3 || args[0] is not ("seed" or "upgrade"))
    throw new ArgumentException("Expected seed|upgrade, private runtime directory, and source checkout root.");
var root = Path.GetFullPath(args[1]);
var repository = new ProfileRepository(new()
{
    DatabasePath = Path.Combine(root, "profiles.db"),
    CheckoutRoot = args[2]
}, new WindowsDpapiPayloadProtector());
await repository.InitializeAsync();
var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
if (args[0] == "upgrade")
{
    var current = await repository.GetLatestAsync(id) ?? throw new InvalidOperationException("Missing fixture profile.");
    if (!current.Synthetic || current.Version != 1) throw new InvalidOperationException("Only the version-one synthetic fixture can be upgraded.");
    var result = await repository.ApplyPatchAsync(new()
    {
        ProfileId = id,
        BaseVersion = current.Version,
        ProposedAt = DateTimeOffset.UtcNow,
        ProposedProfile = current with { FullName = "Synthetic Fixture Revision Two" }
    }, locallyApproved: true);
    if (!result.Applied || result.Profile?.Version != 2) throw new InvalidOperationException("Fixture profile upgrade failed.");
    Console.WriteLine("Synthetic profile upgraded locally to version 2.");
}
else
{
    var now = DateTimeOffset.UtcNow;
    var profile = ProfilePolicy.ConfirmLocally(SyntheticData.Profile(), now,
        ProfileField.FullName, ProfileField.Email, ProfileField.Salary);
    await repository.SaveInitialAsync(profile);
    var journal = new ApplicationJournal(Path.Combine(root, "synthetic-applications.db"));
    await journal.InitializeAsync();
    var ready = new ApplicationDraft
    {
        Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ProfileId = id,
        ProfileVersion = 1,
        JobKey = "plugin-fixture-ready",
        JobTitle = "Synthetic C# Engineer",
        Employer = "Synthetic Employer",
        RecipientOrigin = "http://127.0.0.1:5179",
        ResumeRef = "synthetic-resume.txt",
        ResumeHash = "SYNTHETIC-NOT-A-REAL-CV-HASH",
        Status = ApplicationStatus.ReadyForDataSharing,
        Synthetic = true
    };
    await journal.CreateAsync(new(ready));
    var completed = ready with
    {
        Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        JobKey = "plugin-fixture-completed",
        Status = ApplicationStatus.SubmittedVerified
    };
    await journal.CreateAsync(new(completed, Evidence: new(
        "receipt-plugin-fixture-1", "application-plugin-fixture-1", completed.ResumeHash, now)));
    Console.WriteLine("Synthetic fixture seeded: profile v1, ready application, and seeded receipt application.");
}
SqliteConnection.ClearAllPools();
