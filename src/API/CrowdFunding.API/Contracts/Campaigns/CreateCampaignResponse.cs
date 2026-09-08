namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload returned upon campaign creation.
/// </summary>
/// <param name="CampaignId">The unique identifier of the newly created campaign.</param>
public sealed record CreateCampaignResponse(Guid CampaignId);
