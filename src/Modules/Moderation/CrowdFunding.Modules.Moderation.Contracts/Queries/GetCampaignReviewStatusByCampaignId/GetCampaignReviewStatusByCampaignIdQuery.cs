namespace CrowdFunding.Modules.Moderation.Contracts.Queries.GetCampaignReviewStatusByCampaignId;

/// <summary>
/// Represents the request to execute the Get Campaign Review Status By Campaign Id query.
/// </summary>
public sealed record GetCampaignReviewStatusByCampaignIdQuery(Guid CampaignId);
