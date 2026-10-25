namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.ConfirmPledgePayment;

/// <summary>
/// Represents the request to execute the Confirm Pledge Payment use case.
/// </summary>
public sealed record ConfirmPledgePaymentCommand(
    Guid CampaignId,
    Guid PledgeId,
    string PaymentReference);
