using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Pledges.Contracts.Events.PayoutPaid;

/// <summary>Published when the creator of a succeeded campaign has been paid.</summary>
public sealed class PayoutPaidApplicationEvent : BaseApplicationEvent
{
    public PayoutPaidApplicationEvent(Guid payoutId, Guid campaignId, Guid ownerId, decimal grossAmount, decimal feeAmount, decimal netAmount, string currency)
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
