namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload for Publish Campaign.
/// </summary>
public sealed record PublishCampaignResponse(Guid CampaignId, string Status);
