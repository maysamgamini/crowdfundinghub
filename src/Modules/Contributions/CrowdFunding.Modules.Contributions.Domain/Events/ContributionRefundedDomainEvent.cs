using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Contributions.Domain.Events;

/// <summary>
/// Represents the domain event raised when a contribution is refunded as part of the
/// compensation saga triggered by a campaign failing or being cancelled.
/// </summary>
public sealed class ContributionRefundedDomainEvent : BaseEvent
{
    public ContributionRefundedDomainEvent(Guid contributionId, Guid campaignId, Guid contributorId, decimal amount, string currency)
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
