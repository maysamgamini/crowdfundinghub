using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

namespace CrowdFunding.Modules.Moderation.Application.Abstractions.Services;

/// <summary>
/// Defines read-model queries for moderation review data.
/// </summary>
public interface ICampaignReviewReadService
{
    Task<GetCampaignReviewByCampaignIdResult?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);
}
