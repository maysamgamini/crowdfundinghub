namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

public sealed record RejectCampaignReviewCommand(
    Guid CampaignId,
    Guid ModeratorId,
    string? Notes);
