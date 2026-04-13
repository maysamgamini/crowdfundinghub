namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

/// <summary>
/// Represents the outcome returned by Reject Campaign Review.
/// </summary>
public sealed record RejectCampaignReviewResult(Guid CampaignId, string Status);
