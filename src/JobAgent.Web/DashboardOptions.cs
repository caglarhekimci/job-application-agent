namespace JobAgent.Web;

public sealed record DashboardOptions
{
    public string DataDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JobApplicationAgent", "demo");
    public string CareerOrigin { get; init; } = "http://127.0.0.1:5179";
    public string BootstrapToken { get; init; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    public string? WebRootPath { get; init; }
    public bool EnableSyntheticCommands { get; init; }
    public string BridgeToken { get; init; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    public Guid BridgeInstanceId { get; init; } = Guid.NewGuid();
    public DateTimeOffset BridgeExpiresAt { get; init; } = DateTimeOffset.UtcNow.AddHours(8);
}
