using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;

namespace CrowdFunding.ArchitectureTests;

/// <summary>
/// Guards TICKET-023: Pledges must validate pledges against its own locally replicated
/// campaign state, never a synchronous, in-process cross-module read into Campaigns.
/// </summary>
public sealed class ReplicatedReadModelBoundaryTests
{
    [Fact]
    public void CampaignsContracts_ShouldNotExposeASynchronousCrossModuleReadServiceForPledgeAvailability()
    {
        // The anti-pattern this ticket eliminated (ICampaignPledgeAvailabilityReader /
        // GetCampaignPledgeAvailabilityQuery) lived in exactly this namespace. Asserting
        // the namespace no longer exists in the assembly at all is a stronger guarantee than
        // checking Pledges.Application's reference list, since Pledges.Application
        // legitimately references Campaigns.Contracts for the application *events* it replicates
        // from — an assembly-level "does not reference" check would not distinguish the two.
        var campaignsContractsTypes = typeof(CampaignCreatedApplicationEvent).Assembly.GetTypes();

        Assert.DoesNotContain(
            campaignsContractsTypes,
            type => type.Namespace is not null
                && (type.Namespace.Contains("GetCampaignPledgeAvailability")
                    || type.Namespace.Contains("ReadServices")));
    }

    [Fact]
    public void MakePledgeCommandHandler_ShouldNotDependOnCampaignsContractsQueryTypes()
    {
        var constructorParameterTypes = typeof(MakePledgeCommandHandler)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType);

        Assert.DoesNotContain(
            constructorParameterTypes,
            type => type.Namespace is not null && type.Namespace.StartsWith("CrowdFunding.Modules.Campaigns", StringComparison.Ordinal));
    }
}
