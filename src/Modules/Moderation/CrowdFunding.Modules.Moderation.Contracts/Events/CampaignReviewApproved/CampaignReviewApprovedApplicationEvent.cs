using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Moderation.Contracts.Events.CampaignReviewApproved;

/// <summary>
/// Represents the application event published when Campaign Review Approved.
/// </summary>
public sealed class CampaignReviewApprovedApplicationEvent : BaseApplicationEvent
{
    public CampaignReviewApprovedApplicationEvent(Guid campaignId, Guid moderatorId, string? notes)
    {
        CampaignId = campaignId;
        ModeratorId = moderatorId;
        Notes = notes;
    }

    public Guid CampaignId { get; }
    public Guid ModeratorId { get; }
    public string? Notes { get; }
}
