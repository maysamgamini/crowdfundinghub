namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Queries.GetCampaignPayout;

/// <summary>The request to read a campaign's payout: what the creator is owed, what was paid, and the ledger behind it.</summary>
public sealed record GetCampaignPayoutQuery(Guid CampaignId);
