using System.Reflection;
using CrowdFunding.Modules.Notifications.Domain.Aggregates;
using CrowdFunding.Modules.Notifications.Application.Events;

namespace CrowdFunding.ArchitectureTests;

public sealed class NotificationsModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(NotificationPreference).Assembly);

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
