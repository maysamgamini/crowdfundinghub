namespace CrowdFunding.API.Contracts.Moderation;

/// <summary>
/// Represents the HTTP response payload containing moderation review details for a campaign.
/// </summary>
/// <param name="CampaignId">The unique identifier of the reviewed campaign.</param>
/// <param name="Status">The current moderation status of the review (e.g. Approved, Rejected, Pending).</param>
/// <param name="ModeratorId">The unique identifier of the moderator who completed the review, if reviewed.</param>
/// <param name="Notes">The review notes or feedback left by the moderator.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the moderation review was initiated.</param>
/// <param name="ReviewedAtUtc">The UTC timestamp when the review was completed, if finalized.</param>
public sealed record CampaignReviewResponse(
    Guid CampaignId,
    string Status,
    Guid? ModeratorId,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc);
