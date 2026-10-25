using System.Reflection;
using CrowdFunding.API.Controllers;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;
using CrowdFunding.Modules.Pledges.Infrastructure.Persistence.Repositories;

namespace CrowdFunding.ArchitectureTests;

public sealed class PledgesModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(Pledge).Assembly);

        Assert.DoesNotContain(typeof(MakePledgeCommand).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(PledgeRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldReference_DomainButNotInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(MakePledgeCommand).Assembly);

        Assert.Contains(typeof(Pledge).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(PledgeRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Api_ShouldNotReference_PledgeDomainDirectly()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(PledgesController).Assembly);

        Assert.DoesNotContain(typeof(Pledge).Assembly.GetName().Name, referencedAssemblyNames);
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
