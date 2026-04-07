namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

public sealed record FailContributionPaymentCommand(
    Guid CampaignId,
    Guid ContributionId,
    string FailureReason);
