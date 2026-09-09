using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Contributions.Domain.Events;

/// <summary>
/// Represents the domain event raised when a contribution payment fails (TICKET-052).
/// Enables immediate release of reserved reward tier inventory without waiting for the 15-minute scavenger.
/// </summary>
public sealed class ContributionPaymentFailedDomainEvent : BaseEvent
{
    public ContributionPaymentFailedDomainEvent(
        Guid contributionId,
        Guid campaignId,
        Guid contributorId,
        decimal amount,
        string currency,
        string failureReason,
        Guid? rewardTierReservationId = null)
    {
        ContributionId = contributionId;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Amount = amount;
        Currency = currency;
        FailureReason = failureReason;
        RewardTierReservationId = rewardTierReservationId;
    }

    public Guid ContributionId { get; }
    public Guid CampaignId { get; }
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string FailureReason { get; }
    public Guid? RewardTierReservationId { get; }
}
