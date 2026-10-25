using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Pledges.Domain.Events;

/// <summary>The backer's card was saved: the pledge now counts toward the campaign.</summary>
public sealed class PledgeActivatedDomainEvent : BaseEvent
{
    public PledgeActivatedDomainEvent(
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
