namespace CrowdFunding.API.Contracts.Campaigns;

public sealed record CancelCampaignResponse(Guid CampaignId, string Status);
