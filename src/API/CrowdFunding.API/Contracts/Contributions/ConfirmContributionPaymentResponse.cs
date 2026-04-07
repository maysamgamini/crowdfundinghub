namespace CrowdFunding.API.Contracts.Contributions;

public sealed record ConfirmContributionPaymentResponse(
    Guid ContributionId,
    string Status,
    string PaymentReference);
