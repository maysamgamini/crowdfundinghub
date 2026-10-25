using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Pledges.Domain.Events;

/// <summary>
/// Represents the domain event raised when a pledge is refunded as part of the
/// compensation saga triggered by a campaign failing or being cancelled.
/// </summary>
public sealed class PledgeRefundedDomainEvent : BaseEvent
{
    public PledgeRefundedDomainEvent(
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
    public Guid? RewardTierReservationId { get; }
}
