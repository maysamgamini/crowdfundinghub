namespace CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

/// <summary>
/// Represents the outcome returned by Get Campaign Review Status By Campaign Id.
/// </summary>
public sealed record GetCampaignReviewStatusByCampaignIdResult(Guid CampaignId, string Status);
