namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.ApproveCampaignReview;

/// <summary>
/// Represents the request to execute the Approve Campaign Review use case.
/// </summary>
public sealed record ApproveCampaignReviewCommand(
    Guid CampaignId,
    string? Notes);
