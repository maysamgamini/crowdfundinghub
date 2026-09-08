using System.Reflection;
using CrowdFunding.Modules.Notifications.Application.Events;

namespace CrowdFunding.ArchitectureTests;

public sealed class NotificationsModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        // Notifications.Domain currently has no public types of its own to anchor a typeof()
        // lookup (it's an empty shell module, per improvement.md §2.9), so it's loaded by name
        // instead — the ProjectReference guarantees it's on disk next to the test binary.
        var domainAssembly = Assembly.Load("CrowdFunding.Modules.Notifications.Domain");
        var referencedAssemblyNames = GetReferencedAssemblyNames(domainAssembly);

        Assert.DoesNotContain(typeof(CampaignPublishedNotificationHandler).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldNotReference_OtherModulesInternals()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(CampaignPublishedNotificationHandler).Assembly);

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
            "CrowdFunding.Modules.CampaignUpdates.Application",
            "CrowdFunding.Modules.CampaignUpdates.Infrastructure",
            "CrowdFunding.Modules.CampaignUpdates.Domain",
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
