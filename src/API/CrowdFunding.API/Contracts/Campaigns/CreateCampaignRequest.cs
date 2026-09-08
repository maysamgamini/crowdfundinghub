namespace CrowdFunding.API.Contracts.Campaigns;

/// <summary>
/// Represents the HTTP request payload for creating a new crowdfunding campaign.
/// </summary>
/// <param name="Title">The title of the campaign.</param>
/// <param name="Story">The detailed background story and description of the campaign.</param>
/// <param name="Category">The category of the campaign (e.g. Technology, Community, Art).</param>
/// <param name="GoalAmount">The financial target funding goal amount (must be greater than 0).</param>
/// <param name="Currency">The three-letter ISO 4217 currency code (e.g. USD, EUR).</param>
/// <param name="DeadlineUtc">The UTC deadline date and time when the campaign concludes.</param>
public sealed record CreateCampaignRequest(
    string Title,
    string Story,
    string Category,
    decimal GoalAmount,
    string Currency,
    DateTime DeadlineUtc);
