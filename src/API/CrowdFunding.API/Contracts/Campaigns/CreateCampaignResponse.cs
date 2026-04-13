namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload for Create Campaign.
/// </summary>
public sealed record CreateCampaignResponse(Guid CampaignId);
