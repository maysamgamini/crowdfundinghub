using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Moderation.Application.Abstractions.Services;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.ListCampaignReviews;

/// <summary>
/// Handles List Campaign Reviews query requests.
/// </summary>
public sealed class ListCampaignReviewsQueryHandler : IQueryHandler<ListCampaignReviewsQuery, PagedResult<GetCampaignReviewByCampaignIdResult>>
{
    private readonly ICampaignReviewReadService _campaignReviewReadService;

    public ListCampaignReviewsQueryHandler(ICampaignReviewReadService campaignReviewReadService)
    {
        _campaignReviewReadService = campaignReviewReadService;
    }

    public async Task<PagedResult<GetCampaignReviewByCampaignIdResult>> Handle(
        ListCampaignReviewsQuery query,
        CancellationToken cancellationToken)
    {
        return await _campaignReviewReadService.ListAsync(query.PageRequest, query.Status, cancellationToken);
    }
}
