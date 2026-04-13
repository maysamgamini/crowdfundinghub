using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>
/// Represents the domain event raised when Campaign Created.
/// </summary>
public sealed class CampaignCreatedDomainEvent : BaseEvent
{
    public CampaignCreatedDomainEvent(Guid campaignId, Guid ownerId)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
}
