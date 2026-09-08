namespace CrowdFunding.API.Contracts.Moderation;

/// <summary>
/// Represents the HTTP request payload for reviewing a campaign (approval or rejection).
/// </summary>
/// <param name="Notes">Optional reviewer notes, feedback, or justification for the moderation action.</param>
public sealed record ReviewCampaignRequest(string? Notes);
