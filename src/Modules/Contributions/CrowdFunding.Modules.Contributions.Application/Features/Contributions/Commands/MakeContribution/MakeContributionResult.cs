namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.MakeContribution;

/// <summary>
/// Represents the outcome returned by Make Contribution.
/// </summary>
public sealed record MakeContributionResult(
    Guid ContributionId,
    string Status);
