namespace CrowdFunding.API.Contracts.Moderation;

public sealed record CampaignReviewResponse(
    Guid CampaignId,
    string Status,
    Guid? ModeratorId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
