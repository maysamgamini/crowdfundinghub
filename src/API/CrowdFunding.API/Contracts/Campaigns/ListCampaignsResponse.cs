namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents a campaign summary item in a paginated list response.
/// </summary>
/// <param name="Id">The unique identifier of the campaign.</param>
/// <param name="OwnerId">The unique identifier of the campaign owner.</param>
/// <param name="Title">The title of the campaign.</param>
/// <param name="Category">The category of the campaign.</param>
/// <param name="GoalAmount">The target funding goal amount.</param>
/// <param name="GoalCurrency">The currency code of the funding goal.</param>
/// <param name="RaisedAmount">The current funding amount raised.</param>
/// <param name="RaisedCurrency">The currency code of the raised funds.</param>
/// <param name="DeadlineUtc">The UTC deadline when the campaign concludes.</param>
/// <param name="Status">The current status of the campaign.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the campaign was created.</param>
public sealed record ListCampaignsResponse(
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
