using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;

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
