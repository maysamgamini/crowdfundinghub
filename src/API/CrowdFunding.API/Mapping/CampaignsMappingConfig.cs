using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.API.Contracts.RewardTiers;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.CreateRewardTier;
using CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.PublishCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using Mapster;

namespace CrowdFunding.API.Mapping;

/// <summary>
/// Registers Mapster mappings for Campaigns.
/// </summary>
public static class CampaignsMappingConfig
{
    /// <summary>
    /// Registers Mapster type mappings between API contracts and Campaigns application models.
    /// </summary>
    /// <param name="config">The Mapster configuration instance.</param>
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CancelCampaignResult, CancelCampaignResponse>();
        config.NewConfig<CreateCampaignRequest, CreateCampaignCommand>();
        config.NewConfig<CreateCampaignResult, CreateCampaignResponse>();
        config.NewConfig<PublishCampaignResult, PublishCampaignResponse>();
        config.NewConfig<GetCampaignByIdResult, GetCampaignByIdResponse>();
        config.NewConfig<ListCampaignsResult, ListCampaignsResponse>();
        config.NewConfig<CreateRewardTierResult, CreateRewardTierResponse>();
        config.NewConfig<ReserveRewardTierSlotResult, ReserveRewardTierSlotResponse>();
    }
}
