using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;

public sealed class ContributionPaymentConfirmedApplicationEvent : BaseApplicationEvent
{
    public ContributionPaymentConfirmedApplicationEvent(Guid contributionId, Guid campaignId, decimal amount, string currency)
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
