using System.Reflection;

namespace JobAgent.Infrastructure.Tests;

public sealed class FoundationInfrastructureContractTests
{
    [Fact]
    public void ProfileRepository_PublicContractExists()
    {
        var infrastructure = Assembly.Load("JobAgent.Infrastructure");

        var repository = infrastructure.GetType("JobAgent.Infrastructure.Storage.ProfileRepository");
        var importer = infrastructure.GetType("JobAgent.Infrastructure.Documents.ResumeImporter");

        Assert.NotNull(repository);
        Assert.NotNull(importer);
    }
}
