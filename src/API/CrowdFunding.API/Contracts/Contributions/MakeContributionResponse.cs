namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP response payload returned upon creating a contribution.
/// </summary>
/// <param name="ContributionId">The unique identifier of the created contribution.</param>
/// <param name="Status">The initial status of the contribution (Pending).</param>
public sealed record MakeContributionResponse(
    Guid ContributionId,
    string Status);
