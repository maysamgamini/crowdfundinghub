namespace CrowdFunding.Modules.Moderation.Contracts.Commands.CreateCampaignReview;

public sealed record CreateCampaignReviewResult(
    Guid CampaignReviewId,
    string Status);
