namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;

/// <summary>
/// Represents the outcome returned by Confirm Pledge Payment.
/// </summary>
public sealed record ConfirmPledgePaymentResult(
    Guid PledgeId,
    string Status,
    string PaymentReference);
