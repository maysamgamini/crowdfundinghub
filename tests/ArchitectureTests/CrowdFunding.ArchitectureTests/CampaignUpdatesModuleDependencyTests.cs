using System.Reflection;
using CrowdFunding.Modules.CampaignUpdates.Application.Events;

namespace CrowdFunding.ArchitectureTests;

public sealed class CampaignUpdatesModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        // CampaignUpdates.Domain currently has no public types of its own to anchor a typeof()
        // lookup (it's an empty shell module, per improvement.md §2.9), so it's loaded by name
        // instead — the ProjectReference guarantees it's on disk next to the test binary.
        var domainAssembly = Assembly.Load("CrowdFunding.Modules.CampaignUpdates.Domain");
        var referencedAssemblyNames = GetReferencedAssemblyNames(domainAssembly);

        Assert.DoesNotContain(typeof(CampaignCreatedActivityHandler).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldNotReference_OtherModulesInternals()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(CampaignCreatedActivityHandler).Assembly);

        string[] forbiddenAssemblies =
        [
            "CrowdFunding.Modules.Campaigns.Application",
            "CrowdFunding.Modules.Campaigns.Infrastructure",
            "CrowdFunding.Modules.Campaigns.Domain",
            "CrowdFunding.Modules.Contributions.Application",
            "CrowdFunding.Modules.Contributions.Infrastructure",
            "CrowdFunding.Modules.Contributions.Domain",
            "CrowdFunding.Modules.Moderation.Application",
            "CrowdFunding.Modules.Moderation.Infrastructure",
            "CrowdFunding.Modules.Moderation.Domain",
            "CrowdFunding.Modules.Identity.Application",
            "CrowdFunding.Modules.Identity.Infrastructure",
            "CrowdFunding.Modules.Identity.Domain",
            "CrowdFunding.Modules.Notifications.Application",
            "CrowdFunding.Modules.Notifications.Infrastructure",
            "CrowdFunding.Modules.Notifications.Domain",
        ];

        foreach (var forbidden in forbiddenAssemblies)
        {
            Assert.DoesNotContain(forbidden, referencedAssemblyNames);
        }
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
