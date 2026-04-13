namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload for Make Contribution.
/// </summary>
public sealed record MakeContributionResponse(
    Guid ContributionId,
    string Status);
