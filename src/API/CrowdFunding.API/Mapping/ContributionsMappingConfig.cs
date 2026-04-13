using CrowdFunding.API.Contracts.Contributions;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;
using CrowdFunding.Modules.Contributions.Application.Features.Contributions.Queries.ListContributionsByCampaign;
using Mapster;

namespace CrowdFunding.API.Mapping;

/// <summary>
/// Registers Mapster mappings for Contributions.
/// </summary>
public static class ContributionsMappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ConfirmContributionPaymentResult, ConfirmContributionPaymentResponse>();
        config.NewConfig<FailContributionPaymentResult, FailContributionPaymentResponse>();
        config.NewConfig<MakeContributionResult, MakeContributionResponse>();
        config.NewConfig<ListContributionsByCampaignResult, ListContributionsResponse>();
    }
}
