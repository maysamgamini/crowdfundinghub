namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

/// <summary>
/// Represents the outcome returned by Add Contribution To Campaign.
/// </summary>
public sealed record AddContributionToCampaignResult(
    Guid CampaignId,
    decimal RaisedAmount,
    string Currency);
