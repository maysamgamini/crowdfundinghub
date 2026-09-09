using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>
/// Represents the domain event raised when Campaign Created.
/// </summary>
public sealed class CampaignCreatedDomainEvent : BaseEvent
{
    public CampaignCreatedDomainEvent(Guid campaignId, Guid ownerId, string title, string currency, DateTime deadlineUtc)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
        Title = title;
        Currency = currency;
        DeadlineUtc = deadlineUtc;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }

    /// <summary>Carried so consumers (e.g. Contributions' replicated active-campaign cache) can
    /// build their own local read model entirely from this event — Event-Carried State Transfer —
    /// instead of calling back into Campaigns synchronously.</summary>
    public string Title { get; }
    public string Currency { get; }
    public DateTime DeadlineUtc { get; }
}
