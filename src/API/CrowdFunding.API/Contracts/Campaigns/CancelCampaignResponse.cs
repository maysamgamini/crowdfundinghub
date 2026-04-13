namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload for Cancel Campaign.
/// </summary>
public sealed record CancelCampaignResponse(Guid CampaignId, string Status);
