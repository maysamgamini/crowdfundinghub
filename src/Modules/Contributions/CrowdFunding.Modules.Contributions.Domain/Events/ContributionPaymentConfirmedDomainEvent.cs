using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Contributions.Domain.Events;

/// <summary>
/// Represents the domain event raised when Contribution Payment Confirmed.
/// </summary>
public sealed class ContributionPaymentConfirmedDomainEvent : BaseEvent
{
    public ContributionPaymentConfirmedDomainEvent(
        Guid contributionId, Guid campaignId, Guid contributorId, decimal amount, string currency, Guid? rewardTierReservationId = null)
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
