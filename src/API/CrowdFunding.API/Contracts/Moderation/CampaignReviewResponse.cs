namespace CrowdFunding.API.Contracts.Moderation;

/// <summary>
/// Represents the HTTP response payload for Campaign Review.
/// </summary>
public sealed record CampaignReviewResponse(
    Guid CampaignId,
    string Status,
    Guid? ModeratorId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
