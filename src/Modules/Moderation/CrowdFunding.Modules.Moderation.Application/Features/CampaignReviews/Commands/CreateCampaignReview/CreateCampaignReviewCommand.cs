namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;

/// <summary>
/// Represents the request to execute the Create Campaign Review use case.
/// </summary>
public sealed record CreateCampaignReviewCommand(Guid CampaignId);
