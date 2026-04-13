namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;

/// <summary>
/// Represents the outcome returned by Create Campaign Review.
/// </summary>
public sealed record CreateCampaignReviewResult(Guid CampaignReviewId, string Status);
