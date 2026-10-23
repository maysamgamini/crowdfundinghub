using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>A confirmed contribution reached a campaign that no longer accepts funds, so the campaign refused to count it.</summary>
public sealed class LateContributionRejectedDomainEvent : BaseEvent
{
    public LateContributionRejectedDomainEvent(Guid campaignId, Guid contributionId, decimal amount, string currency, string campaignStatus, DateTime occurredOnUtc)
    {
        CampaignId = campaignId;
        ContributionId = contributionId;
        Amount = amount;
        Currency = currency;
        CampaignStatus = campaignStatus;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid CampaignId { get; }
    public Guid ContributionId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string CampaignStatus { get; }
    public DateTime OccurredOnUtc { get; }
}
