namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Commands.FailContributionPayment;

/// <summary>
/// Represents the request to execute the Fail Contribution Payment use case.
/// </summary>
public sealed record FailContributionPaymentCommand(
    Guid CampaignId,
    Guid ContributionId,
    string FailureReason);
