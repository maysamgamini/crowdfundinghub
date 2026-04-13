namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

/// <summary>
/// Represents the outcome returned by Approve Campaign Review.
/// </summary>
public sealed record ApproveCampaignReviewResult(Guid CampaignId, string Status);
