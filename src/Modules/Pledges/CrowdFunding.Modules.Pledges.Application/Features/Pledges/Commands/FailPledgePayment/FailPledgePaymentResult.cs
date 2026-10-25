namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;

/// <summary>
/// Represents the outcome returned by Fail Pledge Payment.
/// </summary>
public sealed record FailPledgePaymentResult(
    Guid PledgeId,
    string Status,
    string FailureReason);
