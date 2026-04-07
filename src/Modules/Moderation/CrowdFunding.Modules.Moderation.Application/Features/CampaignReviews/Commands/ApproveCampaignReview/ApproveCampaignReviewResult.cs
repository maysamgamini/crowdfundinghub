namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

public sealed record ApproveCampaignReviewResult(Guid CampaignId, string Status);
