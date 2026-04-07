namespace CrowdFunding.API.Contracts.Campaigns;

public sealed record CreateCampaignRequest(
    string Title,
    string Story,
    string Category,
    decimal GoalAmount,
    string Currency,
    DateTime DeadlineUtc);
