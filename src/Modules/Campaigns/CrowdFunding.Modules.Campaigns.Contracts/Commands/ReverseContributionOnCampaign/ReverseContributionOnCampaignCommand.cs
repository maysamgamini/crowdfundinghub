namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.ReverseContributionOnCampaign;

/// <summary>Takes a refunded contribution out of a campaign's books with a compensating ledger entry.</summary>
public sealed record ReverseContributionOnCampaignCommand(
    Guid CampaignId,
    Guid ContributionId,
    decimal Amount,
    string Currency);
