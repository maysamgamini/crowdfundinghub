using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Pledges.Domain.Events;

/// <summary>A pledge was released because the campaign ended without being funded, or was cancelled. Nothing was charged.</summary>
public sealed class PledgeReleasedDomainEvent : BaseEvent
{
    public PledgeReleasedDomainEvent(
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
