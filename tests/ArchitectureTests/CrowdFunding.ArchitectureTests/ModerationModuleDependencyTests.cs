using System.Reflection;
using CrowdFunding.API.Controllers;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;
using CrowdFunding.Modules.Moderation.Domain.Aggregates;
using CrowdFunding.Modules.Moderation.Infrastructure.Persistence.Repositories;

namespace CrowdFunding.ArchitectureTests;

public sealed class ModerationModuleDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(CampaignReview).Assembly);

        Assert.DoesNotContain(typeof(ApproveCampaignReviewCommand).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(CampaignReviewRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Application_ShouldReference_DomainButNotInfrastructure()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(ApproveCampaignReviewCommand).Assembly);

        Assert.Contains(typeof(CampaignReview).Assembly.GetName().Name, referencedAssemblyNames);
        Assert.DoesNotContain(typeof(CampaignReviewRepository).Assembly.GetName().Name, referencedAssemblyNames);
    }

    [Fact]
    public void Api_ShouldNotReference_ModerationDomainDirectly()
    {
        var referencedAssemblyNames = GetReferencedAssemblyNames(typeof(ModerationController).Assembly);

        Assert.DoesNotContain(typeof(CampaignReview).Assembly.GetName().Name, referencedAssemblyNames);
    }

    private static IReadOnlyCollection<string> GetReferencedAssemblyNames(Assembly assembly)
    {
        return assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();
    }
}
