using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;

/// <summary>
/// Represents the application event published when Contribution Refunded.
/// </summary>
public sealed class ContributionRefundedApplicationEvent : BaseApplicationEvent
{
    public ContributionRefundedApplicationEvent(Guid contributionId, Guid campaignId, Guid contributorId, decimal amount, string currency)
    {
        ContributionId = contributionId;
        CampaignId = campaignId;
        ContributorId = contributorId;
        Amount = amount;
        Currency = currency;
    }

    public Guid ContributionId { get; }
    public Guid CampaignId { get; }
    public Guid ContributorId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
}
