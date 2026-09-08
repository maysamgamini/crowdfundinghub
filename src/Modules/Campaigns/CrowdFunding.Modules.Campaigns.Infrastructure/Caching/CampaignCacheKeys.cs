namespace CrowdFunding.Modules.Campaigns.Infrastructure.Caching;

/// <summary>
/// Shared so the read-side cache (<see cref="CachedCampaignReadService"/>) and the write-side
/// invalidation (<see cref="Persistence.Repositories.CampaignRepository"/>) always agree on the
/// key for a given campaign.
/// </summary>
internal static class CampaignCacheKeys
{
    /// <summary>
    /// Computes the distributed cache key for campaign detail queries.
    /// </summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <returns>The formatted cache key string.</returns>
    public static string Details(Guid campaignId) => $"campaigns:{campaignId}:details";
}
