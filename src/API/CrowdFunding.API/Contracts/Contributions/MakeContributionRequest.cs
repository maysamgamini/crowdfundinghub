namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP request payload for Make Contribution.
/// </summary>
public sealed record MakeContributionRequest(
    decimal Amount,
    string Currency);
