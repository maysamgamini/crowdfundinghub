using CrowdFunding.API.Contracts.Campaigns;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using Mapster;

namespace CrowdFunding.API.Mapping;

public static class CampaignsMappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateCampaignRequest, CreateCampaignCommand>();

        config.NewConfig<CreateCampaignResult, CreateCampaignResponse>();

        config.NewConfig<GetCampaignByIdResult, GetCampaignByIdResponse>();
    }
}