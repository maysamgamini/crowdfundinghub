using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentFailed;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Events;

/// <summary>
/// Immediately releases a held reward tier reservation when payment fails (card declined, fraud check failure)
/// rather than waiting for the 15-minute scavenger sweep, closing the denial-of-inventory window (TICKET-052).
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

            var rewardTier = await _rewardTierRepository.GetByIdAsync(freshReservation.RewardTierId, ct);
            if (rewardTier is not null)
            {
                rewardTier.ReleaseReservation();
                await _rewardTierRepository.UpdateAsync(rewardTier, ct);
            }

            freshReservation.Release();
            await _reservationRepository.UpdateAsync(freshReservation, ct);
        }, cancellationToken);
    }
}
