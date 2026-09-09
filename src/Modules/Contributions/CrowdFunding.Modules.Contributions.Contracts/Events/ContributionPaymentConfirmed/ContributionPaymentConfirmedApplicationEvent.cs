using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;

/// <summary>
/// Represents the application event published when Contribution Payment Confirmed.
/// </summary>
public sealed class ContributionPaymentConfirmedApplicationEvent : BaseApplicationEvent
{
    public ContributionPaymentConfirmedApplicationEvent(Guid contributionId, Guid campaignId, Guid contributorId, decimal amount, string currency)
    {
        ContributionId = contributionId;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Amount = amount;
        Currency = currency;
    }

    public Guid ContributionId { get; }
    public Guid CampaignId { get; }

    /// <summary>Carried so consumers (e.g. Notifications' receipt email) can identify the
    /// recipient without a synchronous cross-module call — TICKET-031.</summary>
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
}
