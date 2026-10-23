using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.LateContributionRejected;

/// <summary>
/// A campaign refused to count a contribution because it had already ended (succeeded, failed or been
/// cancelled) by the time the payment was confirmed. The Contributions module reacts by refunding it.
/// </summary>
public sealed class LateContributionRejectedApplicationEvent : BaseApplicationEvent
{
    public LateContributionRejectedApplicationEvent(Guid campaignId, Guid contributionId, decimal amount, string currency, string campaignStatus, DateTime occurredOnUtc)
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
