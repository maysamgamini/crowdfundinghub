namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

public sealed record FailContributionPaymentResult(
    Guid ContributionId,
    string Status,
    string FailureReason);
