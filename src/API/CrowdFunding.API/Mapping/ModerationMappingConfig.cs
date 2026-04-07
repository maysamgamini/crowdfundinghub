using CrowdFunding.API.Contracts.Moderation;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;
using Mapster;

namespace CrowdFunding.API.Mapping;

public static class ModerationMappingConfig
{
    public static void Register(TypeAdapterConfig config)
    {
        config.NewConfig<GetCampaignReviewByCampaignIdResult, CampaignReviewResponse>();
    }
}
