using System.Reflection;
using CrowdFunding.API.Controllers;
using CrowdFunding.Modules.Identity.Application.Features.Users.Commands.RegisterUser;
using CrowdFunding.Modules.Identity.Domain.Aggregates;
using CrowdFunding.Modules.Identity.Infrastructure.Persistence.Repositories;

namespace CrowdFunding.ArchitectureTests;

public sealed class IdentityModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(User).Assembly);

        Assert.DoesNotContain(typeof(RegisterUserCommand).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(UserRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldReference_DomainButNotInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(RegisterUserCommand).Assembly);

        Assert.Contains(typeof(User).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(UserRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Api_ShouldNotReference_IdentityDomainDirectly()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(IdentityController).Assembly);

        Assert.DoesNotContain(typeof(User).Assembly.GetName().Name, referencedAssemblyNames);
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
