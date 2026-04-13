using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;

/// <summary>
/// Represents the application event published when Campaign Published.
/// </summary>
public sealed class CampaignPublishedApplicationEvent : BaseApplicationEvent
{
    public CampaignPublishedApplicationEvent(Guid campaignId, Guid ownerId)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
}
