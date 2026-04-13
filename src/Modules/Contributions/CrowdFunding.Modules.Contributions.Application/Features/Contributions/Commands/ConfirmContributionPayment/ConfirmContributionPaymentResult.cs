namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

/// <summary>
/// Represents the outcome returned by Confirm Contribution Payment.
/// </summary>
public sealed record ConfirmContributionPaymentResult(
    Guid ContributionId,
    string Status,
    string PaymentReference);
