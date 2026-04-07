namespace CrowdFunding.API.Contracts.Contributions;

public sealed record MakeContributionResponse(
    Guid ContributionId,
    string Status);
