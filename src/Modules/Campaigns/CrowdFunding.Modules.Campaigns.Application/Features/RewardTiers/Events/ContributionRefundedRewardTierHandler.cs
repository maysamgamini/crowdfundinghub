using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Events;

/// <summary>
/// Reclaims confirmed reward tier claims when a contribution is refunded as part of the campaign
/// failure or cancellation saga (TICKET-051). Decrements <see cref="RewardTier.ClaimedCount"/> and
/// transitions the reservation to Released.
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

    public async Task Handle(ContributionRefundedApplicationEvent notification, CancellationToken cancellationToken)
    {
        if (notification.RewardTierReservationId is null)
        {
            return;
        }

        var reservationId = notification.RewardTierReservationId.Value;
        var probe = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken);
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
                return;
            }

            var rewardTier = await _rewardTierRepository.GetByIdAsync(freshReservation.RewardTierId, ct);
            if (rewardTier is not null)
            {
                rewardTier.ReleaseClaim();
                await _rewardTierRepository.UpdateAsync(rewardTier, ct);
            }

            freshReservation.Refund();
            await _reservationRepository.UpdateAsync(freshReservation, ct);
        }, cancellationToken);
    }
}
