using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;

/// <summary>
/// Represents the application event published when Campaign Cancelled.
/// </summary>
public sealed class CampaignCancelledApplicationEvent : BaseApplicationEvent
{
    public CampaignCancelledApplicationEvent(Guid campaignId, Guid ownerId)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
}
