using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;

/// <summary>
/// Represents the application event published when Contribution Refunded.
/// </summary>
public sealed class ContributionRefundedApplicationEvent : BaseApplicationEvent
{
    public ContributionRefundedApplicationEvent(
        Guid contributionId,
        Guid campaignId,
        Guid contributorId,
        decimal amount,
        string currency,
        Guid? rewardTierReservationId = null)
    {
        ContributionId = contributionId;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Amount = amount;
        Currency = currency;
        RewardTierReservationId = rewardTierReservationId;
    }

    public Guid ContributionId { get; }
    public Guid CampaignId { get; }
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>
    /// The reward tier slot reservation, if any, associated with this refunded contribution (TICKET-051).
    /// </summary>
    public Guid? RewardTierReservationId { get; }
}
