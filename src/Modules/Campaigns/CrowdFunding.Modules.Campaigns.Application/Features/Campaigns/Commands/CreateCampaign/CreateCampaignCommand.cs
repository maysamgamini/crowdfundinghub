namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

/// <summary>
/// Represents the request to execute the Create Campaign use case.
/// </summary>
public sealed record CreateCampaignCommand(
    string Title,
    string Story,
    string Category,
    decimal GoalAmount,
    string Currency,
    DateTime DeadlineUtc);
