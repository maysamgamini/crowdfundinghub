using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using Mapster;

namespace CrowdFunding.API.Mapping;

public static class ContributionsMappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<MakeContributionResult, MakeContributionResponse>();
        config.NewConfig<ListContributionsByCampaignResult, ListContributionsResponse>();
    }
}
