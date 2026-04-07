namespace CrowdFunding.API.Contracts.Contributions;

public sealed record ListContributionsResponse(
    Guid Id,
    Guid CampaignId,
    Guid ContributorId,
    decimal Amount,
    string Currency,
    DateTime CreatedAtUtc);
