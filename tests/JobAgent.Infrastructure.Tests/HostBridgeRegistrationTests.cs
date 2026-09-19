using System.Text;
using JobAgent.Infrastructure.Bridge;

namespace JobAgent.Infrastructure.Tests;

public sealed class HostBridgeRegistrationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "jobagent-bridge-" + Guid.NewGuid());

    [Fact]
    public async Task RegistrationIsProtectedBoundedAndOwnerRemoved()
    {
        var now = DateTimeOffset.UtcNow;
        var value = new HostBridgeRegistration(new Uri("http://127.0.0.1:54321/"), new string('A', 64), Guid.NewGuid(), now.AddHours(1));
        await HostBridgeRegistrationStore.WriteAsync(root, value);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, HostBridgeRegistrationStore.FileName));
        Assert.DoesNotContain(value.Token, Encoding.UTF8.GetString(bytes));
        Assert.Equal(value, await HostBridgeRegistrationStore.ReadAsync(root, now));
        await HostBridgeRegistrationStore.RemoveIfOwnedAsync(root, Guid.NewGuid());
        Assert.NotNull(await HostBridgeRegistrationStore.ReadAsync(root, now));
        await HostBridgeRegistrationStore.RemoveIfOwnedAsync(root, value.InstanceId);
        Assert.Null(await HostBridgeRegistrationStore.ReadAsync(root, now));
    }

    [Theory]
    [InlineData("http://localhost:1234/")]
    [InlineData("https://127.0.0.1:1234/")]
    [InlineData("http://127.0.0.1:1234/not-root")]
    [InlineData("http://127.0.0.1:1234/?token=a")]
    [InlineData("http://user@127.0.0.1:1234/")]
    public async Task UntrustedEndpointCannotBeRegistered(string endpoint)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => HostBridgeRegistrationStore.WriteAsync(root,
            new(new Uri(endpoint), new string('A', 64), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1))));
    }

    [Fact]
    public async Task ExpiredOrCorruptRegistrationFailsClosed()
    {
        var now = DateTimeOffset.UtcNow;
        await HostBridgeRegistrationStore.WriteAsync(root, new(new Uri("http://127.0.0.1:1234/"),
            new string('B', 64), Guid.NewGuid(), now.AddMinutes(1)));
        Assert.Null(await HostBridgeRegistrationStore.ReadAsync(root, now.AddMinutes(2)));
        await File.WriteAllTextAsync(Path.Combine(root, HostBridgeRegistrationStore.FileName), "untrusted");
        Assert.Null(await HostBridgeRegistrationStore.ReadAsync(root, now));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
