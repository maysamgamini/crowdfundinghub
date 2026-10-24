using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Contributions.Domain.Events;

/// <summary>The backer's card was saved: the pledge now counts toward the campaign.</summary>
public sealed class ContributionPledgeActivatedDomainEvent : BaseEvent
{
    public ContributionPledgeActivatedDomainEvent(
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
