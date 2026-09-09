using System.Reflection;
using CrowdFunding.Moderation.Service.Controllers;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// Guards TICKET-029's core claim: <c>CrowdFunding.Moderation.Service</c> is a standalone host
/// that runs the unchanged <c>CrowdFunding.Modules.Moderation.*</c> assemblies with zero
/// reference to the monolith's API project or to any OTHER module's Application, Domain, or
/// Infrastructure assembly. A reference to another module's thin Contracts assembly (e.g.
/// Campaigns.Contracts, or the transitive Identity.Contracts pulled in by
/// Moderation.Application for permission/claim constants) is the intentional integration seam
/// and is not flagged — only Application/Domain/Infrastructure assemblies, and the monolith host
/// itself, would represent real coupling.
/// </summary>
public sealed class ModerationServiceExtractionBoundaryTests
{
    private static readonly string[] ForbiddenAssemblyNameFragments =
    [
        "CrowdFunding.API",
        "CrowdFunding.Modules.Identity.Application",
        "CrowdFunding.Modules.Identity.Domain",
        "CrowdFunding.Modules.Identity.Infrastructure",
        "CrowdFunding.Modules.Campaigns.Application",
        "CrowdFunding.Modules.Campaigns.Domain",
        "CrowdFunding.Modules.Campaigns.Infrastructure",
        "CrowdFunding.Modules.Contributions",
        "CrowdFunding.Modules.Notifications",
        "CrowdFunding.Modules.CampaignUpdates",
    ];

    [Fact]
    public void ModerationServiceAssembly_ShouldNotReferenceTheMonolithOrAnyOtherModulesApplicationDomainOrInfrastructure()
    {
        var serviceAssembly = typeof(ModerationReviewsController).Assembly;

        var referencedAssemblyNames = serviceAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        foreach (var forbiddenFragment in ForbiddenAssemblyNameFragments)
        {
            Assert.DoesNotContain(
                referencedAssemblyNames,
                name => name.StartsWith(forbiddenFragment, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ModerationServiceAssembly_ShouldOnlyReferenceItsOwnModuleAndCampaignsContracts()
    {
        var serviceAssembly = typeof(ModerationReviewsController).Assembly;

        var crowdFundingReferences = serviceAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("CrowdFunding.", StringComparison.Ordinal))
            .Cast<string>()
            .ToArray();

        Assert.All(crowdFundingReferences, name =>
            Assert.True(
                name.StartsWith("CrowdFunding.Modules.Moderation", StringComparison.Ordinal)
                    || name.StartsWith("CrowdFunding.Modules.Campaigns.Contracts", StringComparison.Ordinal)
                    || name.StartsWith("CrowdFunding.Modules.Identity.Contracts", StringComparison.Ordinal)
                    || name.StartsWith("CrowdFunding.BuildingBlocks", StringComparison.Ordinal),
                $"Unexpected cross-module reference: {name}"));
    }
}
