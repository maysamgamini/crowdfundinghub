using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Moderation.Domain.Events;

/// <summary>
/// Represents the domain event raised when Campaign Review Rejected.
/// </summary>
public sealed class CampaignReviewRejectedDomainEvent : BaseEvent
{
    public CampaignReviewRejectedDomainEvent(Guid campaignId, Guid moderatorId, string? notes)
    {
        CampaignId = campaignId;
        ModeratorId = moderatorId;
        Notes = notes;
    }

    public Guid CampaignId { get; }
    public Guid ModeratorId { get; }
    public string? Notes { get; }
}
