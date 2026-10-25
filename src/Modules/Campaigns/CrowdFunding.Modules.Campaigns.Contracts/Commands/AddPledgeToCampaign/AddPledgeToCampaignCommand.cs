namespace CrowdFunding.Modules.Campaigns.Contracts.Commands.AddPledgeToCampaign;

/// <summary>
/// Represents the request to execute the Add Pledge To Campaign use case.
/// </summary>
public sealed record AddPledgeToCampaignCommand(
    Guid CampaignId,
    Guid PledgeId,
    decimal Amount,
    string Currency,
    Guid? RewardTierReservationId = null);
