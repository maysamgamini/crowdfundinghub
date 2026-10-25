using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Pledges.Domain.Events;

/// <summary>Raised when the payment provider has paid a campaign's creator.</summary>
public sealed class PayoutPaidDomainEvent : BaseEvent
{
    public PayoutPaidDomainEvent(Guid payoutId, Guid campaignId, Guid ownerId, decimal grossAmount, decimal feeAmount, decimal netAmount, string currency)
    {
        PayoutId = payoutId;
        CampaignId = campaignId;
        OwnerId = ownerId;
        GrossAmount = grossAmount;
        FeeAmount = feeAmount;
        NetAmount = netAmount;
        Currency = currency;
    }

    public Guid PayoutId { get; }
    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
    public decimal GrossAmount { get; }
    public decimal FeeAmount { get; }
    public decimal NetAmount { get; }
    public string Currency { get; }
}
