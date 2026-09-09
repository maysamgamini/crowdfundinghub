namespace CrowdFunding.Modules.Notifications.Infrastructure.Persistence.ReadModels;

/// <summary>
/// Notifications' own local replica of a campaign's title and owner — populated by
/// <c>ReplicatedCampaignTitleEventHandler</c> reacting to Campaigns' application events, never by
/// a synchronous cross-module read. See TICKET-031 / <c>ICampaignTitleCacheRepository</c>.
/// </summary>
public sealed class CampaignTitleCache
{
    public Guid CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
