using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgeSetup;
using CrowdFunding.API.Contracts.Pledges;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetPledgeById;
using CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.ListPledgesByCampaign;
using Mapster;

namespace CrowdFunding.API.Mapping;

/// <summary>
/// Registers Mapster mappings for Pledges.
/// </summary>
public static class PledgesMappingConfig
{
    /// <summary>
    /// Registers Mapster type mappings between API contracts and Pledges application models.
    /// </summary>
    /// <param name="config">The Mapster configuration instance.</param>
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ConfirmPledgePaymentResult, ConfirmPledgePaymentResponse>();
        config.NewConfig<ConfirmPledgeSetupResult, ConfirmPledgeSetupResponse>();
        config.NewConfig<FailPledgePaymentResult, FailPledgePaymentResponse>();
        config.NewConfig<MakePledgeResult, MakePledgeResponse>();
        config.NewConfig<ListPledgesByCampaignResult, ListPledgesResponse>();
        config.NewConfig<GetPledgeByIdResult, GetPledgeByIdResponse>();
    }
}
