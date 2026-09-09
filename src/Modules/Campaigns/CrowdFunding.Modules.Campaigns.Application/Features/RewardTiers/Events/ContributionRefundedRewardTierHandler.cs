using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Events;

/// <summary>
/// Handles <see cref="ContributionRefundedApplicationEvent"/> to reverse reward tier claims
/// when a contribution is refunded during campaign failure or cancellation (TICKET-051).
/// Completes the compensation saga for campaign cancellations and failures by ensuring
/// reward tier claimed counts are decremented and capacity is restored.
/// </summary>
public sealed class ContributionRefundedRewardTierHandler : IEventHandler<ContributionRefundedApplicationEvent>
{
    private readonly IRewardTierReservationRepository _reservationRepository;
    private readonly IRewardTierRepository _rewardTierRepository;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public ContributionRefundedRewardTierHandler(
        IRewardTierReservationRepository reservationRepository,
        IRewardTierRepository rewardTierRepository,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _reservationRepository = reservationRepository;
        _rewardTierRepository = rewardTierRepository;
        _transactionExecutor = transactionExecutor;
    }

    /// <inheritdoc/>
    public async Task Handle(ContributionRefundedApplicationEvent notification, CancellationToken cancellationToken)
    {
        if (notification.RewardTierReservationId is null)
        {
            return;
        }

        var reservationId = notification.RewardTierReservationId.Value;
        var probe = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);

        // If probe is null or not in Confirmed status (e.g. already Released/Refunded), return idempotently
        if (probe is null || probe.Status != RewardTierReservationStatus.Confirmed)
        {
            return;
        }

        var tierLockKey = AdvisoryLockKey.FromGuid(probe.RewardTierId);

        await _transactionExecutor.ExecuteAsync(tierLockKey, async ct =>
        {
            var freshReservation = await _reservationRepository.GetByIdAsync(reservationId, ct);
            if (freshReservation is null || freshReservation.Status != RewardTierReservationStatus.Confirmed)
            {
                // Idempotent: already refunded or released
                return;
            }

            var rewardTier = await _rewardTierRepository.GetByIdAsync(freshReservation.RewardTierId, ct)
                ?? throw new KeyNotFoundException($"Reward tier '{freshReservation.RewardTierId}' was not found.");

            freshReservation.Refund();
            rewardTier.ReleaseClaim();

            await _rewardTierRepository.UpdateAsync(rewardTier, ct);
            await _reservationRepository.UpdateAsync(freshReservation, ct);
        }, cancellationToken);
    }
}
