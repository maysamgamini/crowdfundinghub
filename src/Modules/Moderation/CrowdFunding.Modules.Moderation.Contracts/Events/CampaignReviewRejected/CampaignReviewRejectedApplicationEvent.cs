using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewRejected;

/// <summary>
/// Represents the application event published when Campaign Review Rejected.
/// </summary>
public sealed class CampaignReviewRejectedApplicationEvent : BaseApplicationEvent
{
    public CampaignReviewRejectedApplicationEvent(Guid campaignId, Guid moderatorId, string? notes)
    {
        CampaignId = campaignId;
        ModeratorId = moderatorId;
        Notes = notes;
    }

    public Guid CampaignId { get; }
    public Guid ModeratorId { get; }
    public string? Notes { get; }
}
