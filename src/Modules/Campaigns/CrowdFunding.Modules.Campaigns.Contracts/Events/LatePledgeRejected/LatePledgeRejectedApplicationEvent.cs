using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.LatePledgeRejected;

/// <summary>
/// A campaign refused to count a pledge because it had already ended (succeeded, failed or been
/// cancelled) by the time the payment was confirmed. The Pledges module reacts by refunding it.
/// </summary>
public sealed class LatePledgeRejectedApplicationEvent : BaseApplicationEvent
{
    public LatePledgeRejectedApplicationEvent(Guid campaignId, Guid pledgeId, decimal amount, string currency, string campaignStatus, DateTime occurredOnUtc)
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
