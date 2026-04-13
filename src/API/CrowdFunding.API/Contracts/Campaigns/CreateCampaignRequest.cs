namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP request payload for Create Campaign.
/// </summary>
public sealed record CreateCampaignRequest(
    string Title,
    string Story,
    string Category,
    decimal GoalAmount,
    string Currency,
    DateTime DeadlineUtc);
