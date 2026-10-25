namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddPledgeToCampaign;

/// <summary>
/// Represents the outcome returned by Add Pledge To Campaign.
/// </summary>
public sealed record AddPledgeToCampaignResult(
    Guid CampaignId,
    decimal RaisedAmount,
    string Currency,
    bool Accepted = true);
