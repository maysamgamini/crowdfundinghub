namespace CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

public sealed record GetCampaignReviewStatusByCampaignIdResult(
    Guid CampaignId,
    string Status);
