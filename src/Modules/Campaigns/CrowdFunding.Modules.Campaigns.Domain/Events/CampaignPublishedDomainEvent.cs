using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

public sealed class CampaignPublishedDomainEvent : BaseEvent
{
    public CampaignPublishedDomainEvent(Guid campaignId, Guid ownerId)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
}