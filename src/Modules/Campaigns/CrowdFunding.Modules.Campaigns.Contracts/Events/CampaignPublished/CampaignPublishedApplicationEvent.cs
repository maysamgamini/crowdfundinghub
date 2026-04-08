using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;

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
