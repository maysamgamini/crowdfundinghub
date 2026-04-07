using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

public sealed class GetCampaignReviewByCampaignIdQueryHandler
{
    private readonly ICampaignReviewReadService _campaignReviewReadService;

    public GetCampaignReviewByCampaignIdQueryHandler(ICampaignReviewReadService campaignReviewReadService)
    {
        _campaignReviewReadService = campaignReviewReadService;
    }

    public async Task<GetCampaignReviewByCampaignIdResult> Handle(
        GetCampaignReviewByCampaignIdQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _campaignReviewReadService.GetByCampaignIdAsync(query.CampaignId, cancellationToken);

        return result ?? throw new KeyNotFoundException($"Campaign review for campaign '{query.CampaignId}' was not found.");
    }
}
