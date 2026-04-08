using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewStatusByCampaignId;

public sealed class GetCampaignReviewStatusByCampaignIdQueryHandler : IQueryHandler<GetCampaignReviewStatusByCampaignIdQuery, GetCampaignReviewStatusByCampaignIdResult>
{
    private readonly ICampaignReviewReadService _campaignReviewReadService;

    public GetCampaignReviewStatusByCampaignIdQueryHandler(ICampaignReviewReadService campaignReviewReadService)
    {
        _campaignReviewReadService = campaignReviewReadService;
    }

    public async Task<GetCampaignReviewStatusByCampaignIdResult> Handle(
        GetCampaignReviewStatusByCampaignIdQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _campaignReviewReadService.GetByCampaignIdAsync(query.CampaignId, cancellationToken);

        if (result is null)
        {
            throw new KeyNotFoundException($"Campaign review for campaign '{query.CampaignId}' was not found.");
        }

        return new GetCampaignReviewStatusByCampaignIdResult(
            result.CampaignId,
            result.Status);
    }
}
