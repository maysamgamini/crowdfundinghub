using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Pledges.Contracts.Events.PledgeReleased;

/// <summary>A pledge was released: the campaign ended without being funded, or was cancelled. Nothing was ever charged, so nothing is refunded.</summary>
public sealed class PledgeReleasedApplicationEvent : BaseApplicationEvent
{
    public PledgeReleasedApplicationEvent(
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
