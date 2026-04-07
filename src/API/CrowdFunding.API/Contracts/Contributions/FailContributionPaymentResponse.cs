namespace CrowdFunding.API.Contracts.Contributions;

public sealed record FailContributionPaymentResponse(
    Guid ContributionId,
    string Status,
    string FailureReason);
