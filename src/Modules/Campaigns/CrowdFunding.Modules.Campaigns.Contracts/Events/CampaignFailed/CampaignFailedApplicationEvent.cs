using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;

/// <summary>
/// Represents the application event published when Campaign Failed. Contributions consumes this
/// to trigger the refund compensation saga for every backer of the campaign.
/// </summary>
public sealed class CampaignFailedApplicationEvent : BaseApplicationEvent
{
    public CampaignFailedApplicationEvent(Guid campaignId, Guid ownerId, decimal raisedAmount, decimal goalAmount, string currency, DateTime occurredOnUtc)
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
