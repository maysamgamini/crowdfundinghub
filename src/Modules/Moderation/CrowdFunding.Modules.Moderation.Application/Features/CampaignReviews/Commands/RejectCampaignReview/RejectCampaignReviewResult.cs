namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

public sealed record RejectCampaignReviewResult(Guid CampaignId, string Status);
