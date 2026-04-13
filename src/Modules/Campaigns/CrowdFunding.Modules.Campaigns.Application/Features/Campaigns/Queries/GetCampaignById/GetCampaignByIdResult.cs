namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;

/// <summary>
/// Represents the outcome returned by Get Campaign By Id.
/// </summary>
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
