namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.ConfirmContributionPayment;

public sealed record ConfirmContributionPaymentCommand(
    Guid CampaignId,
    Guid ContributionId,
    string PaymentReference);
