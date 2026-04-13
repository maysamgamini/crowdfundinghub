namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

/// <summary>
/// Represents the outcome returned by Fail Contribution Payment.
/// </summary>
public sealed record FailContributionPaymentResult(
    Guid ContributionId,
    string Status,
    string FailureReason);
