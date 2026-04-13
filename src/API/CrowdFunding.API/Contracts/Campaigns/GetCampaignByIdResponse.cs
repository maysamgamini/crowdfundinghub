namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload for Get Campaign By Id.
/// </summary>
public sealed record GetCampaignByIdResponse(
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
