namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

public sealed record AddContributionToCampaignCommand(
    Guid CampaignId,
    decimal Amount,
    string Currency);
