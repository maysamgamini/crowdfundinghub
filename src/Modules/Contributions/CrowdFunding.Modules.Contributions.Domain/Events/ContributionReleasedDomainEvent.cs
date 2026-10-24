using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Contributions.Domain.Events;

/// <summary>A pledge was released because the campaign ended without being funded, or was cancelled. Nothing was charged.</summary>
public sealed class ContributionReleasedDomainEvent : BaseEvent
{
    public ContributionReleasedDomainEvent(
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
    public Guid? RewardTierReservationId { get; }
}
