using JobAgent.Infrastructure.Storage;

namespace JobAgent.Infrastructure.Tests;

internal sealed class TemporaryStore : IDisposable
{
    public TemporaryStore()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "job-agent-foundation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }
    public string DatabasePath => Path.Combine(DirectoryPath, "profiles.db");

    public ProfileStoreOptions Options(bool allowPlaintext = false) => new()
    {
        DatabasePath = DatabasePath,
        CheckoutRoot = Environment.CurrentDirectory,
        AllowSyntheticPlaintextForLinuxTests = allowPlaintext
    };

    public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
}
