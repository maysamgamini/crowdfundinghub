namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP response payload containing full details of a specific campaign.
/// </summary>
/// <param name="Id">The unique identifier of the campaign.</param>
/// <param name="OwnerId">The unique identifier of the user who owns the campaign.</param>
/// <param name="Title">The title of the campaign.</param>
/// <param name="Story">The detailed story and description of the campaign.</param>
/// <param name="Category">The category of the campaign.</param>
/// <param name="GoalAmount">The target funding goal amount.</param>
/// <param name="GoalCurrency">The currency code of the target funding goal.</param>
/// <param name="RaisedAmount">The total funding amount raised to date.</param>
/// <param name="RaisedCurrency">The currency code of the raised funds.</param>
/// <param name="DeadlineUtc">The UTC deadline when the campaign ends.</param>
/// <param name="Status">The current status of the campaign (Draft, Published, Completed, Cancelled).</param>
/// <param name="CreatedAtUtc">The UTC timestamp when the campaign was originally created.</param>
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
