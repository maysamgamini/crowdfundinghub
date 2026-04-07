namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

public sealed record ApproveCampaignReviewCommand(
    Guid CampaignId,
    Guid ModeratorId,
    string? Notes);
