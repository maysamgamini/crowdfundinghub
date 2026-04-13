using CrowdFunding.Modules.Moderation.Domain.Aggregates;

namespace CrowdFunding.Modules.Moderation.Application.Abstractions.Persistence;

/// <summary>
/// Defines persistence operations for campaign reviews.
/// </summary>
public interface ICampaignReviewRepository
{
    Task AddAsync(CampaignReview campaignReview, CancellationToken cancellationToken);
    Task<CampaignReview?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);
    Task UpdateAsync(CampaignReview campaignReview, CancellationToken cancellationToken);
}
