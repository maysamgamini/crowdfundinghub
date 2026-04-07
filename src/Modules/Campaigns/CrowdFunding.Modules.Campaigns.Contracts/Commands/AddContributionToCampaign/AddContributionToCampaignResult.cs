namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

public sealed record AddContributionToCampaignResult(
    Guid CampaignId,
    decimal RaisedAmount,
    string Currency);
