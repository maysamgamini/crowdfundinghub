using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;

/// <summary>
/// Represents the application event published when Campaign Created.
/// </summary>
public sealed class CampaignCreatedApplicationEvent : BaseApplicationEvent
{
    public CampaignCreatedApplicationEvent(Guid campaignId, Guid ownerId)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
}
