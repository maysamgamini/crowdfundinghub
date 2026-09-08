namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload returned after cancelling a campaign.
/// </summary>
/// <param name="CampaignId">The unique identifier of the cancelled campaign.</param>
/// <param name="Status">The new lifecycle status of the campaign.</param>
public sealed record CancelCampaignResponse(Guid CampaignId, string Status);
