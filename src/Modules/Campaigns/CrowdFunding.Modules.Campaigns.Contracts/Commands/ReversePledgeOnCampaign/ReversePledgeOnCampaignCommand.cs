namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.ReversePledgeOnCampaign;

/// <summary>Takes a refunded pledge out of a campaign's books with a compensating ledger entry.</summary>
public sealed record ReversePledgeOnCampaignCommand(
    Guid CampaignId,
    Guid PledgeId,
    decimal Amount,
    string Currency);
