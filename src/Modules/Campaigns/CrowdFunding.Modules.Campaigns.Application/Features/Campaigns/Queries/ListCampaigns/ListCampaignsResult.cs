namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;

public sealed record ListCampaignsResult(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Category,
    decimal GoalAmount,
    string GoalCurrency,
    decimal RaisedAmount,
    string RaisedCurrency,
    DateTime DeadlineUtc,
    string Status,
    DateTime CreatedAtUtc);
