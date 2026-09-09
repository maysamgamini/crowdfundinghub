using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;

/// <summary>
/// Represents the application event published when Campaign Created.
/// </summary>
public sealed class CampaignCreatedApplicationEvent : BaseApplicationEvent
{
    public CampaignCreatedApplicationEvent(Guid campaignId, Guid ownerId, string title, string currency, DateTime deadlineUtc)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
        Title = title;
        Currency = currency;
        DeadlineUtc = deadlineUtc;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }

    /// <summary>Carried so consumers can build their own local read model entirely from this
    /// event (Event-Carried State Transfer) instead of calling back into Campaigns synchronously.</summary>
    public string Title { get; }
    public string Currency { get; }
    public DateTime DeadlineUtc { get; }
}
