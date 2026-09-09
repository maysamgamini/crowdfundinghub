using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentFailed;

/// <summary>
/// Represents the application event published when a contribution payment fails (TICKET-052).
/// </summary>
public sealed class ContributionPaymentFailedApplicationEvent : BaseApplicationEvent
{
    public ContributionPaymentFailedApplicationEvent(
        Guid contributionId,
        Guid campaignId,
        Guid contributorId,
        decimal amount,
        string currency,
        string failureReason,
        Guid? rewardTierReservationId = null)
    {
        ContributionId = contributionId;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Amount = amount;
        Currency = currency;
        FailureReason = failureReason;
        RewardTierReservationId = rewardTierReservationId;
    }

    public Guid ContributionId { get; }
    public Guid CampaignId { get; }
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string FailureReason { get; }

    /// <summary>
    /// The reward tier slot reservation, if any, associated with this failed contribution (TICKET-052).
    /// Used by Campaigns to immediately release the held slot back to inventory.
    /// </summary>
    public Guid? RewardTierReservationId { get; }
}
