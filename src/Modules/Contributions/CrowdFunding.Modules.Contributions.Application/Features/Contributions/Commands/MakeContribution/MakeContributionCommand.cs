namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

public sealed record MakeContributionCommand(
    Guid CampaignId,
    Guid ContributorId,
    decimal Amount,
    string Currency);
