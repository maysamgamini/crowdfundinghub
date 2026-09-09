using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReleaseExpiredRewardTierReservation;

/// <summary>
/// Handles Release Expired Reward Tier Reservation command requests.
/// </summary>
public sealed class ReleaseExpiredRewardTierReservationCommandHandler
    : ICommandHandler<ReleaseExpiredRewardTierReservationCommand, ReleaseExpiredRewardTierReservationResult>
{
    private readonly IRewardTierReservationRepository _reservationRepository;
    private readonly IRewardTierRepository _rewardTierRepository;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public ReleaseExpiredRewardTierReservationCommandHandler(
        IRewardTierReservationRepository reservationRepository,
        IRewardTierRepository rewardTierRepository,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _reservationRepository = reservationRepository;
        _rewardTierRepository = rewardTierRepository;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<ReleaseExpiredRewardTierReservationResult> Handle(
        ReleaseExpiredRewardTierReservationCommand command,
        CancellationToken cancellationToken)
    {
        // A first, unlocked read only to discover which tier's advisory lock to take — the
        // authoritative check happens again immediately after acquiring it below, so a stale or
        // already-resolved read here cannot cause a double release (TOCTOU-safe, same pattern as
        // PublishCampaignCommandHandler — TICKET-041).
        var probe = await _reservationRepository.GetByIdAsync(command.ReservationId, cancellationToken);

        if (probe is null || probe.Status != RewardTierReservationStatus.Reserved)
        {
            return new ReleaseExpiredRewardTierReservationResult(command.ReservationId, Released: false);
        }

        var advisoryLockKey = AdvisoryLockKey.FromGuid(probe.RewardTierId);
        var released = false;

        await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
        {
            var reservation = await _reservationRepository.GetByIdAsync(command.ReservationId, ct);

            if (reservation is null || reservation.Status != RewardTierReservationStatus.Reserved)
            {
                // Already confirmed (checkout completed in the meantime) or already released by
                // a concurrent scavenger pass since the probe above — nothing to do.
                return;
            }

            var rewardTier = await _rewardTierRepository.GetByIdAsync(reservation.RewardTierId, ct)
                ?? throw new KeyNotFoundException($"Reward tier '{reservation.RewardTierId}' was not found.");

            rewardTier.ReleaseReservation();
            reservation.Release();

            await _rewardTierRepository.UpdateAsync(rewardTier, ct);
            await _reservationRepository.UpdateAsync(reservation, ct);

            released = true;
        }, cancellationToken);

        return new ReleaseExpiredRewardTierReservationResult(command.ReservationId, released);
    }
}
