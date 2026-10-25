using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>A confirmed pledge reached a campaign that no longer accepts funds, so the campaign refused to count it.</summary>
public sealed class LatePledgeRejectedDomainEvent : BaseEvent
{
    public LatePledgeRejectedDomainEvent(Guid campaignId, Guid pledgeId, decimal amount, string currency, string campaignStatus, DateTime occurredOnUtc)
    {
        CampaignId = campaignId;
        PledgeId = pledgeId;
        Amount = amount;
        Currency = currency;
        CampaignStatus = campaignStatus;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid CampaignId { get; }
    public Guid PledgeId { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public string CampaignStatus { get; }
    public DateTime OccurredOnUtc { get; }
}
