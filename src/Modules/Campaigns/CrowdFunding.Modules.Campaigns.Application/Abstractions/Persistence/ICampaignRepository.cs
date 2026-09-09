using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;

/// <summary>
/// Defines persistence operations for campaign aggregates.
/// </summary>
public interface ICampaignRepository
{
    Task AddAsync(Campaign campaign, CancellationToken cancellationToken);
    Task<Campaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken);
    Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken);

    /// <summary>Ids of Published campaigns whose deadline has passed as of <paramref name="asOfUtc"/>,
    /// for <c>CampaignExpirationBackgroundService</c> to resolve to Successful or Failed. Returns
    /// only ids — each is re-loaded and locked individually inside
    /// <c>CompleteCampaignCommandHandler</c>/<c>FailCampaignCommandHandler</c>'s advisory-lock
    /// transaction, so a campaign published between this scan and that lock being taken (or one
    /// already resolved by a concurrent worker) can never be double-processed.</summary>
    Task<IReadOnlyList<Guid>> GetExpiredPublishedCampaignIdsAsync(DateTime asOfUtc, CancellationToken cancellationToken);
}
