namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;

public sealed record GetCampaignByIdResult(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Story,
    string Category,
    decimal GoalAmount,
    string GoalCurrency,
    decimal RaisedAmount,
    string RaisedCurrency,
    DateTime DeadlineUtc,
    string Status,
    DateTime CreatedAtUtc);