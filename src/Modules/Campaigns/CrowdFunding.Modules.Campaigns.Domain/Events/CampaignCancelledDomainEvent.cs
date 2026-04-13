using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>
/// Represents the domain event raised when Campaign Cancelled.
/// </summary>
public sealed class CampaignCancelledDomainEvent : BaseEvent
{
    public CampaignCancelledDomainEvent(Guid campaignId, Guid ownerId)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
}
