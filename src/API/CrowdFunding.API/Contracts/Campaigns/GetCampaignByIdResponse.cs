namespace CrowdFunding.API.Contracts.Campaigns;

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