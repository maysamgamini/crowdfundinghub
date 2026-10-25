using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentConfirmed;

/// <summary>
/// Represents the application event published when Pledge Payment Confirmed.
/// </summary>
public sealed class PledgePaymentConfirmedApplicationEvent : BaseApplicationEvent
{
    public PledgePaymentConfirmedApplicationEvent(
        Guid pledgeId, Guid campaignId, Guid backerId, decimal amount, string currency, Guid? rewardTierReservationId = null)
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

    /// <summary>Carried so consumers (e.g. Notifications' receipt email) can identify the
    /// recipient without a synchronous cross-module call — TICKET-031.</summary>
    public Guid BackerId { get; }
    public decimal Amount { get; }
    public string Currency { get; }

    /// <summary>Set when the backer selected a reward perk at checkout — Campaigns uses this to
    /// convert the reservation into a claim on its own <c>RewardTier</c> aggregate (not visible
    /// here) once payment is confirmed. See TICKET-043.</summary>
    public Guid? RewardTierReservationId { get; }
}
