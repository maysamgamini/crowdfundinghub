namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

/// <summary>
/// Represents the request to execute the Confirm Contribution Payment use case.
/// </summary>
public sealed record ConfirmContributionPaymentCommand(
    Guid CampaignId,
    Guid ContributionId,
    string PaymentReference);
