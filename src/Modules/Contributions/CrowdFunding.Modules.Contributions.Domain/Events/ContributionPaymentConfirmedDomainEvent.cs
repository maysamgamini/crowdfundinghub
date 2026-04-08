using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Contributions.Domain.Events;

public sealed class ContributionPaymentConfirmedDomainEvent : BaseEvent
{
    public ContributionPaymentConfirmedDomainEvent(Guid contributionId, Guid campaignId, decimal amount, string currency)
    {
        ContributionId = contributionId;
        CampaignId = campaignId;
        Amount = amount;
        Currency = currency;
    }

    public Guid ContributionId { get; }
    public Guid CampaignId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
}