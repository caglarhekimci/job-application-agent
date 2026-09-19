using JobAgent.Mcp;
using ModelContextProtocol;

namespace JobAgent.Mcp.Tests;

public sealed class ReadOnlyToolTests
{
    [Fact]
    public void RuntimeCapabilities_AreConstrainedAndHonest()
    {
        var tools = new ReadOnlyTools(new RuntimeStore(Path.GetTempPath()));

        var result = tools.RuntimeGetCapabilities();

        Assert.Equal("Fixture", result.Mode);
        Assert.True(result.SyntheticOnly);
        Assert.Equal("Blocked", result.LinkedIn);
        Assert.False(result.PaidApiEnabled);
        Assert.False(result.CanMintApproval);
        Assert.Equal("NotVerifiedOnHost", result.HostExecution);
        Assert.Equal(3, result.ReadOnlyTools.Count);
    }

    [Fact]
    public async Task ProfileSummary_UsesActualVerifiedFacts_AndToolOutputDoesNotLeakPrivateMinimum()
    {
        await using var fixture = new RuntimeFixture();
        await fixture.SeedAsync();
        var tools = new ReadOnlyTools(new RuntimeStore(fixture.DirectoryPath));

        var result = await tools.ProfileGetSummary(fixture.Profile.Id.ToString("D"), CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Equal(fixture.Profile.Version, result.Version);
        Assert.Equal(["C#"], result.VerifiedSkills);
        Assert.DoesNotContain("candidate@example.invalid", json, StringComparison.Ordinal);
        Assert.DoesNotContain("85000", json, StringComparison.Ordinal);
        Assert.DoesNotContain("100000", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceDocumentId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplicationStatus_ContainsStateAndEvidenceWithoutPrivatePayload()
    {
        await using var fixture = new RuntimeFixture();
        await fixture.SeedAsync();
        var tools = new ReadOnlyTools(new RuntimeStore(fixture.DirectoryPath));

        var result = await tools.ApplicationGetStatus(fixture.Application.Draft.Id.ToString("D"));
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Equal("SubmittedVerified", result.State);
        Assert.Equal("receipt-synthetic-1", result.Evidence?.ReceiptId);
        Assert.Equal("application-synthetic-1", result.Evidence?.ApplicationKey);
        Assert.DoesNotContain("SYNTHETIC-HASH", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Answers", json, StringComparison.Ordinal);
        Assert.DoesNotContain("candidate@example.invalid", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../profiles.db")]
    [InlineData("not-a-guid")]
    [InlineData("11111111111111111111111111111111")]
    public async Task ProfileReference_MustBeCanonicalGuid(string invalid)
    {
        var tools = new ReadOnlyTools(new RuntimeStore(Path.GetTempPath()));

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
            tools.ProfileGetSummary(invalid, CancellationToken.None));

        Assert.Equal(McpErrorCode.InvalidParams, exception.ErrorCode);
    }

    [Fact]
    public async Task MissingRuntime_DoesNotCreateDatabase()
    {
        var missing = Path.Combine(Path.GetTempPath(), "job-agent-mcp-missing-" + Guid.NewGuid().ToString("N"));
        var tools = new ReadOnlyTools(new RuntimeStore(missing));

        await Assert.ThrowsAsync<McpProtocolException>(() =>
            tools.ProfileGetSummary(Guid.NewGuid().ToString("D"), CancellationToken.None));

        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task ApplicationReference_MustBeCanonicalGuid()
    {
        var tools = new ReadOnlyTools(new RuntimeStore(Path.GetTempPath()));

        var exception = await Assert.ThrowsAsync<McpProtocolException>(() =>
            tools.ApplicationGetStatus("../synthetic-applications.db"));

        Assert.Equal(McpErrorCode.InvalidParams, exception.ErrorCode);
    }
}
