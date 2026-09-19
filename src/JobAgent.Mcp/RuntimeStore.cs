using JobAgent.Infrastructure.Applications;
using JobAgent.Infrastructure.Storage;

namespace JobAgent.Mcp;

public sealed class RuntimeStore
{
    public const string RuntimeDirectoryEnvironmentVariable = "JOBAGENT_RUNTIME_DIR";

    public RuntimeStore(string runtimeDirectory, bool enableSyntheticCommands = false, bool enableLocalCommands = false)
    {
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            throw new ArgumentException("Runtime directory is required.", nameof(runtimeDirectory));
        if (enableSyntheticCommands && enableLocalCommands)
            throw new ArgumentException("Choose either the local workspace or synthetic command mode.");
        RuntimeDirectory = Path.GetFullPath(runtimeDirectory);
        SyntheticCommandsEnabled = enableSyntheticCommands;
        LocalCommandsEnabled = enableLocalCommands;
    }

    public string RuntimeDirectory { get; }
    public bool SyntheticCommandsEnabled { get; }
    public bool LocalCommandsEnabled { get; }
    public string ProfileDatabasePath => Path.Combine(RuntimeDirectory, "profiles.db");
    public string ApplicationDatabasePath => Path.Combine(RuntimeDirectory, "synthetic-applications.db");

    public static RuntimeStore FromProcessConfiguration()
    {
        var configured = Environment.GetEnvironmentVariable(RuntimeDirectoryEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JobApplicationAgent", "demo")
            : configured;
        return new(path, Environment.GetEnvironmentVariable("JOBAGENT_ENABLE_SYNTHETIC_COMMANDS") == "1",
            Environment.GetEnvironmentVariable("JOBAGENT_ENABLE_LOCAL_COMMANDS") == "1");
    }

    public Task<JobAgent.Core.Profiles.CandidateProfile?> GetProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!File.Exists(ProfileDatabasePath))
            return Task.FromResult<JobAgent.Core.Profiles.CandidateProfile?>(null);
        var repository = new ProfileRepository(new()
        {
            DatabasePath = ProfileDatabasePath,
            CheckoutRoot = AppContext.BaseDirectory
        }, new WindowsDpapiPayloadProtector());
        return repository.GetLatestAsync(id, cancellationToken);
    }

    public Task<WorkflowRecord?> GetApplicationAsync(Guid id)
    {
        if (!File.Exists(ApplicationDatabasePath))
            return Task.FromResult<WorkflowRecord?>(null);
        return new ApplicationJournal(ApplicationDatabasePath).GetAsync(id);
    }
}
