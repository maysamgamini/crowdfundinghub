namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

public sealed record GetCampaignReviewByCampaignIdResult(
    Guid CampaignId,
    string Status,
    Guid? ModeratorId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
