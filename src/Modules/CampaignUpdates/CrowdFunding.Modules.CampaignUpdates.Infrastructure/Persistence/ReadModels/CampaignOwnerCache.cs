namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.ReadModels;

/// <summary>
/// CampaignUpdates' own local replica of a campaign's owner — populated by
/// <c>CampaignCreatedActivityHandler</c> reacting to Campaigns' application events. See
/// TICKET-023 / TICKET-035.
/// </summary>
public sealed class CampaignOwnerCache
{
    public Guid CampaignId { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
