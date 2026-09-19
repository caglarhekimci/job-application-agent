using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JobAgent.Infrastructure.Bridge;

public sealed record HostBridgeRegistration(Uri Origin, string Token, Guid InstanceId, DateTimeOffset ExpiresAt);

// Trusted process configuration only. This credential is never an MCP argument or response.
public static class HostBridgeRegistrationStore
{
    public const string FileName = "host-bridge.dpapi";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JobAgent.HostBridge.v1");

    public static async Task WriteAsync(string directory, HostBridgeRegistration registration)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Bridge registration requires Windows DPAPI.");
        Validate(registration);
        if (registration.ExpiresAt <= DateTimeOffset.UtcNow || registration.ExpiresAt > DateTimeOffset.UtcNow.AddHours(12))
            throw new ArgumentException("Bridge expiry must be within twelve hours.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var temporary = path + "." + registration.InstanceId.ToString("N") + ".tmp";
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(registration), Entropy, DataProtectionScope.CurrentUser);
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static async Task<HostBridgeRegistration?> ReadAsync(string directory, DateTimeOffset now)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var path = Path.Combine(directory, FileName);
            if (!File.Exists(path) || new FileInfo(path).Length is <= 0 or > 8192) return null;
            var bytes = await File.ReadAllBytesAsync(path);
            if (bytes.Length > 8192) return null;
            var registration = JsonSerializer.Deserialize<HostBridgeRegistration>(
                ProtectedData.Unprotect(bytes, Entropy, DataProtectionScope.CurrentUser));
            if (registration is null || registration.ExpiresAt <= now) return null;
            Validate(registration);
            return registration;
        }
        catch (Exception e) when (e is IOException or CryptographicException or JsonException or ArgumentException or UnauthorizedAccessException)
        { return null; }
    }

    public static async Task RemoveIfOwnedAsync(string directory, Guid instanceId)
    {
        var current = await ReadAsync(directory, DateTimeOffset.MinValue);
        if (current?.InstanceId == instanceId) File.Delete(Path.Combine(directory, FileName));
    }

    public static void Validate(HostBridgeRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var uri = registration.Origin;
        if (uri is null || !uri.IsAbsoluteUri || uri.Scheme != "http" || uri.Host != "127.0.0.1" ||
            uri.Port <= 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 || uri.UserInfo.Length != 0 ||
            registration.InstanceId == Guid.Empty || registration.Token is null ||
            registration.Token.Length != 64 || registration.Token.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Invalid loopback bridge registration.");
    }
}
