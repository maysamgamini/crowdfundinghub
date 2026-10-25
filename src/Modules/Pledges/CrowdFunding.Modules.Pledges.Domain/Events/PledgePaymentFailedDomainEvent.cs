using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Pledges.Domain.Events;

/// <summary>
/// Represents the domain event raised when a pledge payment fails (TICKET-052).
/// Enables immediate release of reserved reward tier inventory without waiting for the 15-minute scavenger.
/// </summary>
public sealed class PledgePaymentFailedDomainEvent : BaseEvent
{
    public PledgePaymentFailedDomainEvent(
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
    public Guid? RewardTierReservationId { get; }
}
