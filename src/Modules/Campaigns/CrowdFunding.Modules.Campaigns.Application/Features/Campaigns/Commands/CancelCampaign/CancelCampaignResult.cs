namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CancelCampaign;

public sealed record CancelCampaignResult(Guid CampaignId, string Status);
