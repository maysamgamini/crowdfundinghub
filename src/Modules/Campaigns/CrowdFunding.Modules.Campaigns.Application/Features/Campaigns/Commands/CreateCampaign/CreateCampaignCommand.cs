namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.CreateCampaign;

public sealed record CreateCampaignCommand(
    Guid OwnerId,
    string Title,
    string Story,
    string Category,
    decimal GoalAmount,
    string Currency,
    DateTime DeadlineUtc);