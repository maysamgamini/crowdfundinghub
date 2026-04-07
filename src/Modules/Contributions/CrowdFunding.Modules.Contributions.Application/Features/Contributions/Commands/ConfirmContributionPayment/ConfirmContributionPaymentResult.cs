namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

public sealed record ConfirmContributionPaymentResult(
    Guid ContributionId,
    string Status,
    string PaymentReference);
