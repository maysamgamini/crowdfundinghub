using CrowdFunding.BuildingBlocks.Domain.Common;

namespace CrowdFunding.Modules.Campaigns.Domain.Events;

/// <summary>
/// Represents the domain event raised when a published campaign's deadline passes without
/// reaching its funding goal. Consumed by Contributions to trigger the refund compensation saga.
/// </summary>
public sealed class CampaignFailedDomainEvent : BaseEvent
{
    public CampaignFailedDomainEvent(Guid campaignId, Guid ownerId, decimal raisedAmount, decimal goalAmount, string currency, DateTime occurredOnUtc)
    {
        CampaignId = campaignId;
        OwnerId = ownerId;
        RaisedAmount = raisedAmount;
        GoalAmount = goalAmount;
        Currency = currency;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid CampaignId { get; }
    public Guid OwnerId { get; }
    public decimal RaisedAmount { get; }
    public decimal GoalAmount { get; }
    public string Currency { get; }
    public DateTime OccurredOnUtc { get; }
}
