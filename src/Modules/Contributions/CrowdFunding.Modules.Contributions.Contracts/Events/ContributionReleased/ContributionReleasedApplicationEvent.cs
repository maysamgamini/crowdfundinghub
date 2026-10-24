using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionReleased;

/// <summary>A pledge was released: the campaign ended without being funded, or was cancelled. Nothing was ever charged, so nothing is refunded.</summary>
public sealed class ContributionReleasedApplicationEvent : BaseApplicationEvent
{
    public ContributionReleasedApplicationEvent(
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
