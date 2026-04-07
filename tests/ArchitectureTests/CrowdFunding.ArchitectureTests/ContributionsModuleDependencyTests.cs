using System.Reflection;
using CrowdFunding.API.Controllers;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Infrastructure.Persistence.Repositories;

namespace CrowdFunding.ArchitectureTests;

public sealed class ContributionsModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(Contribution).Assembly);

        Assert.DoesNotContain(typeof(MakeContributionCommand).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(ContributionRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldReference_DomainButNotInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(MakeContributionCommand).Assembly);

        Assert.Contains(typeof(Contribution).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(ContributionRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Api_ShouldNotReference_ContributionDomainDirectly()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(ContributionsController).Assembly);

        Assert.DoesNotContain(typeof(Contribution).Assembly.GetName().Name, referencedAssemblyNames);
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
