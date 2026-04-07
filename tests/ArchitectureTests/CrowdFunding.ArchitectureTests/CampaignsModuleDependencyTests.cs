using System.Reflection;
using CrowdFunding.API.Controllers;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.Repositories;

namespace CrowdFunding.ArchitectureTests;

public sealed class CampaignsModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(Campaign).Assembly);

        Assert.DoesNotContain(typeof(CreateCampaignCommand).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(CampaignRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldReference_DomainButNotInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(CreateCampaignCommand).Assembly);

        Assert.Contains(typeof(Campaign).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(CampaignRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Api_ShouldNotReference_DomainDirectly()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(CampaignsController).Assembly);

        Assert.DoesNotContain(typeof(Campaign).Assembly.GetName().Name, referencedAssemblyNames);
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
