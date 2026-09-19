using JobAgent.Infrastructure.Storage;

namespace JobAgent.Infrastructure.Tests;

public sealed class PayloadProtectionTests
{
    [Fact]
    public void Dpapi_RoundTripsAndChangesBytesOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var protector = new WindowsDpapiPayloadProtector();
        var plaintext = "private profile payload"u8.ToArray();

        var protectedPayload = protector.Protect(plaintext);

        Assert.NotEqual(plaintext, protectedPayload);
        Assert.Equal(plaintext, protector.Unprotect(protectedPayload));
    }

    [Fact]
    public void Dpapi_FailsClosedOutsideWindows()
    {
        if (OperatingSystem.IsWindows())
            return;

        var protector = new WindowsDpapiPayloadProtector();
        Assert.Throws<PlatformNotSupportedException>(() => protector.Protect("secret"u8));
    }
}
