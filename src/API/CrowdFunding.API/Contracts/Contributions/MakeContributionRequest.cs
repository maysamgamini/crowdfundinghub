namespace CrowdFunding.API.Contracts.Contributions;

/// <summary>
/// Represents the HTTP request payload for creating a new contribution pledge to a campaign.
/// </summary>
/// <param name="Amount">The monetary amount to contribute (must be greater than 0).</param>
/// <param name="Currency">The three-letter ISO 4217 currency code (e.g. USD, EUR).</param>
/// <param name="RewardTierReservationId">The reward tier slot reservation id obtained from
/// <c>POST /api/campaigns/{campaignId}/reward-tiers/{rewardTierId}/reserve</c>, if the backer
/// selected a perk. Optional — omit for a plain pledge with no reward tier.</param>
public sealed record MakeContributionRequest(
    decimal Amount,
    string Currency,
    Guid? RewardTierReservationId = null);
