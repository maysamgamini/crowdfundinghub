namespace CrowdFunding.API.Contracts.Moderation;

/// <summary>
/// Represents the HTTP request payload for Review Campaign.
/// </summary>
public sealed record ReviewCampaignRequest(string? Notes);
