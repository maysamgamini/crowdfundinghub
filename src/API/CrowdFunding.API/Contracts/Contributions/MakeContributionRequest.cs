namespace CrowdFunding.API.Contracts.Contributions;

public sealed record MakeContributionRequest(
    decimal Amount,
    string Currency);
