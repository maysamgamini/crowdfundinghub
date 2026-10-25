using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentFailed;

/// <summary>
/// Represents the application event published when a pledge payment fails (TICKET-052).
/// </summary>
public sealed class PledgePaymentFailedApplicationEvent : BaseApplicationEvent
{
    public PledgePaymentFailedApplicationEvent(
        Guid pledgeId,
        Guid campaignId,
        Guid backerId,
        decimal amount,
        string currency,
        string failureReason,
        Guid? rewardTierReservationId = null)
    {
        PledgeId = pledgeId;
        CampaignId = campaignId;
        BackerId = backerId;
        Amount = amount;
        Currency = currency;
        FailureReason = failureReason;
        RewardTierReservationId = rewardTierReservationId;
    }

    public Guid PledgeId { get; }
    public Guid CampaignId { get; }
    public Guid BackerId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string FailureReason { get; }

    /// <summary>
    /// The reward tier slot reservation, if any, associated with this failed pledge (TICKET-052).
    /// Used by Campaigns to immediately release the held slot back to inventory.
    /// </summary>
    public Guid? RewardTierReservationId { get; }
}
