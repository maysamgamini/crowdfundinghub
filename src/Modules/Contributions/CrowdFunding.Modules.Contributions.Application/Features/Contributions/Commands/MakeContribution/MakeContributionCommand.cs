namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

/// <summary>
/// Represents the request to execute the Make Contribution use case.
/// </summary>
public sealed record MakeContributionCommand(
    Guid CampaignId,
    decimal Amount,
    string Currency);
