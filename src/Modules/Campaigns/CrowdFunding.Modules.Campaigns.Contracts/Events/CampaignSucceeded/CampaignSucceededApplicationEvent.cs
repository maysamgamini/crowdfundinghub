using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignSucceeded;

/// <summary>
/// Represents the application event published when Campaign Succeeded.
/// </summary>
public sealed class CampaignSucceededApplicationEvent : BaseApplicationEvent
{
    public CampaignSucceededApplicationEvent(Guid campaignId, Guid ownerId, decimal raisedAmount, string currency, DateTime occurredOnUtc)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
        RaisedAmount = raisedAmount;
        Currency = currency;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
    public decimal RaisedAmount { get; }
    public string Currency { get; }
    public DateTime OccurredOnUtc { get; }
}
