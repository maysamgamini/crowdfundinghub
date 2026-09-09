using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;

/// <summary>
/// Represents the application event published when Contribution Payment Confirmed.
/// </summary>
public sealed class ContributionPaymentConfirmedApplicationEvent : BaseApplicationEvent
{
    public ContributionPaymentConfirmedApplicationEvent(
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

    /// <summary>Carried so consumers (e.g. Notifications' receipt email) can identify the
    /// recipient without a synchronous cross-module call — TICKET-031.</summary>
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>Set when the backer selected a reward perk at checkout — Campaigns uses this to
    /// convert the reservation into a claim on its own <c>RewardTier</c> aggregate (not visible
    /// here) once payment is confirmed. See TICKET-043.</summary>
    public Guid? RewardTierReservationId { get; }
}
