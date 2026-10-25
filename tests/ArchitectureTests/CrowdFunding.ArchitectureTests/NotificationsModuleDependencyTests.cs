using System.Reflection;
using CrowdFunding.Modules.Notifications.Application.Events;
using CrowdFunding.Modules.Notifications.Infrastructure.Features.Preferences;

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
            "CrowdFunding.Modules.Pledges.Application",
            "CrowdFunding.Modules.Pledges.Infrastructure",
            "CrowdFunding.Modules.Pledges.Domain",
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

    [Fact]
    public void Tier2PreferenceHandlers_ShouldOnlyDependOnTheModulesOwnDbContextAndClock()
    {
        var tier2Types = typeof(UnsubscribeCommandHandler).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(UnsubscribeCommandHandler).Namespace && type.IsPublic)
            .ToArray();

        Assert.NotEmpty(tier2Types);

        foreach (var type in tier2Types)
        {
            var dependencies = type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name);

            Assert.All(dependencies, name => Assert.Contains(name, new[] { "NotificationsDbContext", "INotificationsDateTimeProvider" }));
        }
    }

    [Fact]
    public void Infrastructure_ShouldNotReference_OtherModulesInternals()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(UnsubscribeCommandHandler).Assembly);

        var forbidden = referencedAssemblyNames.Where(name =>
            name.StartsWith("CrowdFunding.Modules.", StringComparison.Ordinal)
            && !name.StartsWith("CrowdFunding.Modules.Notifications.", StringComparison.Ordinal)
            && !name.EndsWith(".Contracts", StringComparison.Ordinal));

        Assert.Empty(forbidden);
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
