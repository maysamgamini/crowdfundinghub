using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Moderation.Domain.Events;

public sealed class CampaignReviewApprovedDomainEvent : BaseEvent
{
    public CampaignReviewApprovedDomainEvent(Guid campaignId, Guid moderatorId, string? notes)
    {
        CampaignId = campaignId;
        ModeratorId = moderatorId;
        Notes = notes;
    }

    public Guid CampaignId { get; }
    public Guid ModeratorId { get; }
    public string? Notes { get; }
}