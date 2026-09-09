using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentFailed;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Events;

/// <summary>
/// Handles <see cref="ContributionPaymentFailedApplicationEvent"/> to immediately release
/// reserved reward tier inventory when payment fails, eliminating the 15-minute lockout (TICKET-052).
/// </summary>
public sealed class ContributionPaymentFailedRewardTierHandler : IEventHandler<ContributionPaymentFailedApplicationEvent>
{
    private readonly IRewardTierReservationRepository _reservationRepository;
    private readonly IRewardTierRepository _rewardTierRepository;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public ContributionPaymentFailedRewardTierHandler(
        IRewardTierReservationRepository reservationRepository,
        IRewardTierRepository rewardTierRepository,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _reservationRepository = reservationRepository;
        _rewardTierRepository = rewardTierRepository;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task Handle(ContributionPaymentFailedApplicationEvent notification, CancellationToken cancellationToken)
    {
        if (notification.RewardTierReservationId is null)
        {
            return;
        }

        var reservationId = notification.RewardTierReservationId.Value;
        var probe = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);

        if (probe is null || probe.Status != RewardTierReservationStatus.Reserved)
        {
            // Already released (e.g. by scavenger) or confirmed: nothing to release
            return;
        }

        var tierLockKey = AdvisoryLockKey.FromGuid(probe.RewardTierId);

        await _transactionExecutor.ExecuteAsync(tierLockKey, async ct =>
        {
            var freshReservation = await _reservationRepository.GetByIdAsync(reservationId, ct);
            if (freshReservation is null || freshReservation.Status != RewardTierReservationStatus.Reserved)
            {
                return;
            }

            var rewardTier = await _rewardTierRepository.GetByIdAsync(freshReservation.RewardTierId, ct)
                ?? throw new KeyNotFoundException($"Reward tier '{freshReservation.RewardTierId}' was not found.");

            freshReservation.Release();
            rewardTier.ReleaseReservation();

            await _rewardTierRepository.UpdateAsync(rewardTier, ct);
            await _reservationRepository.UpdateAsync(freshReservation, ct);
        }, cancellationToken);
    }
}
