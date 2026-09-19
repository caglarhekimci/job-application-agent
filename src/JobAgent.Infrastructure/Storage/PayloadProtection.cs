using System.Security.Cryptography;
using System.Text;

namespace JobAgent.Infrastructure.Storage;

public interface IPayloadProtector
{
    bool IsPlaintext { get; }
    byte[] Protect(ReadOnlySpan<byte> plaintext);
    byte[] Unprotect(ReadOnlySpan<byte> protectedPayload);
}

public sealed class WindowsDpapiPayloadProtector : IPayloadProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JobAgent.Profile.v1");

    public bool IsPlaintext => false;

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows DPAPI protection is unavailable on this platform.");
        return ProtectedData.Protect(plaintext.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows DPAPI protection is unavailable on this platform.");
        return ProtectedData.Unprotect(protectedPayload.ToArray(), Entropy, DataProtectionScope.CurrentUser);
    }
}

public sealed class SyntheticPlaintextPayloadProtector : IPayloadProtector
{
    public bool IsPlaintext => true;
    public byte[] Protect(ReadOnlySpan<byte> plaintext) => plaintext.ToArray();
    public byte[] Unprotect(ReadOnlySpan<byte> protectedPayload) => protectedPayload.ToArray();
}
