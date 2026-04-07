namespace CrowdFunding.API.Contracts.Contributions;

public sealed record MakeContributionRequest(
    Guid ContributorId,
    decimal Amount,
    string Currency);
