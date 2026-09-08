namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload returned after publishing a campaign.
/// </summary>
/// <param name="CampaignId">The unique identifier of the published campaign.</param>
/// <param name="Status">The new lifecycle status of the campaign.</param>
public sealed record PublishCampaignResponse(Guid CampaignId, string Status);
