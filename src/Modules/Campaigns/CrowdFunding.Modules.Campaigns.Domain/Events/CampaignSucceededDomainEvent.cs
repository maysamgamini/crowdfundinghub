using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>
/// Represents the domain event raised when a published campaign reaches its funding goal by its deadline.
/// </summary>
public sealed class CampaignSucceededDomainEvent : BaseEvent
{
    public CampaignSucceededDomainEvent(Guid campaignId, Guid ownerId, decimal raisedAmount, string currency, DateTime occurredOnUtc)
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
