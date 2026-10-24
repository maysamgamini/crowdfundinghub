using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPledgeActivated;

/// <summary>The backer's card was saved, so the pledge counts toward the campaign's total. Nothing has been charged yet.</summary>
public sealed class ContributionPledgeActivatedApplicationEvent : BaseApplicationEvent
{
    public ContributionPledgeActivatedApplicationEvent(
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
