namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.RejectCampaignReview;

/// <summary>
/// Represents the request to execute the Reject Campaign Review use case.
/// </summary>
public sealed record RejectCampaignReviewCommand(
    Guid CampaignId,
    string? Notes);
