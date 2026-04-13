namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Queries.GetCampaignReviewByCampaignId;

/// <summary>
/// Represents the request to execute the Get Campaign Review By Campaign Id query.
/// </summary>
public sealed record GetCampaignReviewByCampaignIdQuery(Guid CampaignId);
