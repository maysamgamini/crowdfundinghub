using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Application.Security;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Features.RewardTiers.Commands.ReserveRewardTierSlot;

/// <summary>
/// Reserves one slot on a reward tier. The concurrency-critical operation this ticket exists
/// for: many backers can attempt this on the same tier at once, and the invariant
/// (<c>ClaimedCount + ReservedCount &lt;= TotalCapacity</c>) must hold exactly, not
/// approximately. See TICKET-034.
/// <para>
/// Also creates the <see cref="RewardTierReservation"/> row that correlates this specific hold
/// to the backer and gives it an expiry — TICKET-043's fix for the reservation counter having no
/// way to identify or reclaim an individual abandoned hold. The reservation id returned here is
/// what the client threads through <c>MakeContributionCommand.RewardTierReservationId</c> at
/// checkout.
/// </para>
/// </summary>
public sealed class ReserveRewardTierSlotCommandHandler : ICommandHandler<ReserveRewardTierSlotCommand, ReserveRewardTierSlotResult>
{
    private readonly IRewardTierRepository _rewardTierRepository;
    private readonly IRewardTierReservationRepository _reservationRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICampaignRealtimeNotifier _realtimeNotifier;
    private readonly ICampaignTransactionExecutor _transactionExecutor;

    public ReserveRewardTierSlotCommandHandler(
        IRewardTierRepository rewardTierRepository,
        IRewardTierReservationRepository reservationRepository,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        ICampaignRealtimeNotifier realtimeNotifier,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _rewardTierRepository = rewardTierRepository;
        _reservationRepository = reservationRepository;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotifier = realtimeNotifier;
        _transactionExecutor = transactionExecutor;
    }

    public async Task<ReserveRewardTierSlotResult> Handle(ReserveRewardTierSlotCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The current user must be authenticated to reserve a reward tier slot.");
        }

        // Every concurrent reservation attempt on this tier contends for the same
        // pg_advisory_xact_lock, fully serializing the read-check-increment below — the
        // difference between "50 concurrent requests race an optimistic xmin check and 45 of
        // them fail unpredictably depending on retry timing" and "exactly 5 succeed, 45 get a
        // clean, deterministic 'sold out' rejection," which is what this ticket's stress-test
        // criterion actually demands.
        var advisoryLockKey = AdvisoryLockKey.FromGuid(command.RewardTierId);

        var justSoldOut = false;
        var rewardTierId = command.RewardTierId;
        var availableCount = 0;
        var reservationId = Guid.Empty;
        string title = string.Empty;

        await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
        {
            var rewardTier = await _rewardTierRepository.GetByIdAsync(command.RewardTierId, ct);
            if (rewardTier is null || rewardTier.CampaignId != command.CampaignId)
            {
                throw new KeyNotFoundException($"Reward tier '{command.RewardTierId}' was not found for campaign '{command.CampaignId}'.");
            }

            justSoldOut = rewardTier.ReserveSlot();
            await _rewardTierRepository.UpdateAsync(rewardTier, ct);

            var reservation = RewardTierReservation.Create(
                rewardTier.Id, command.CampaignId, _currentUser.UserId, _dateTimeProvider.UtcNow);
            await _reservationRepository.AddAsync(reservation, ct);
            reservationId = reservation.Id;

            availableCount = rewardTier.AvailableCount;
            title = rewardTier.Title;

            return 0;
        }, cancellationToken);

        if (justSoldOut)
        {
            await _realtimeNotifier.NotifyRewardTierSoldOutAsync(command.CampaignId, rewardTierId, title, cancellationToken);
        }

        return new ReserveRewardTierSlotResult(rewardTierId, availableCount, reservationId);
    }
}
