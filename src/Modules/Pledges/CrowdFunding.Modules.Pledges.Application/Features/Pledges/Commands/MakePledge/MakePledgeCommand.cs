namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Commands.MakePledge;

/// <summary>
/// Represents the request to execute the Make Pledge use case.
/// </summary>
public sealed record MakePledgeCommand(
    Guid CampaignId,
    decimal Amount,
    string Currency,
    Guid? RewardTierReservationId = null,
    string? IdempotencyKey = null);
