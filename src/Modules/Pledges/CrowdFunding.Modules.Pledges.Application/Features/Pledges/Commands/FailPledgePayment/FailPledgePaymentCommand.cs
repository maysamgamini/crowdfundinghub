namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.FailPledgePayment;

/// <summary>
/// Represents the request to execute the Fail Pledge Payment use case.
/// </summary>
public sealed record FailPledgePaymentCommand(
    Guid CampaignId,
    Guid PledgeId,
    string FailureReason);
