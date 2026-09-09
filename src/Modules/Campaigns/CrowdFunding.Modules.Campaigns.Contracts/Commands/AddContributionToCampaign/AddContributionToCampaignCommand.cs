namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

/// <summary>
/// Represents the request to execute the Add Contribution To Campaign use case.
/// </summary>
public sealed record AddContributionToCampaignCommand(
    Guid CampaignId,
    Guid ContributionId,
    decimal Amount,
    string Currency,
    Guid? RewardTierReservationId = null);
