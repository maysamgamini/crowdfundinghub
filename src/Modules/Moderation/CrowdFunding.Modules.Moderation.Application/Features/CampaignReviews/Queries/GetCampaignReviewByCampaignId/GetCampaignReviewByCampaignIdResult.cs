namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

/// <summary>
/// Represents the outcome returned by Get Campaign Review By Campaign Id.
/// </summary>
public sealed record GetCampaignReviewByCampaignIdResult(
    Guid CampaignId,
    string Status,
    Guid? ModeratorId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
