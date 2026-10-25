using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Pledges.Contracts.Events.PledgeRefunded;

/// <summary>
/// Represents the application event published when Pledge Refunded.
/// </summary>
public sealed class PledgeRefundedApplicationEvent : BaseApplicationEvent
{
    public PledgeRefundedApplicationEvent(
        Guid pledgeId,
        Guid campaignId,
        Guid backerId,
        decimal amount,
        string currency,
        Guid? rewardTierReservationId = null)
    {
        PledgeId = pledgeId;
        CampaignId = campaignId;
        BackerId = backerId;
        Amount = amount;
        Currency = currency;
        RewardTierReservationId = rewardTierReservationId;
    }

    public Guid PledgeId { get; }
    public Guid CampaignId { get; }
    public Guid BackerId { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>
    /// The reward tier slot reservation, if any, associated with this refunded pledge (TICKET-051).
    /// </summary>
    public Guid? RewardTierReservationId { get; }
}
