using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Campaigns.Domain.Aggregates;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;

/// <summary>
/// Credits a confirmed contribution to its campaign by appending a pledge entry to the campaign's
/// append-only ledger.
/// </summary>
/// <remarks>
/// <para>
/// Before: the handler read <c>RaisedAmount</c>, added the contribution in code and saved it back,
/// guarded by a dedupe table, an xmin concurrency token and a retry loop. Correctness depended on
/// every code path doing all of that right.
/// </para>
/// <para>
/// Now: the database enforces the invariants. The ledger's unique key on (ContributionId, Kind)
/// makes a redelivered event a no-op, and the raised amount is recomputed as the SUM of the
/// ledger in the same transaction (EF Core cannot write it at all). The per-campaign advisory
/// lock remains the single serialization point: it orders this append against concurrent
/// pledges, refunds, cancellation and completion, so a cancelled campaign is seen as cancelled
/// here and the recomputed sum includes every committed entry.
/// </para>
/// </remarks>
public sealed class AddContributionToCampaignCommandHandler : ICommandHandler<AddContributionToCampaignCommand, AddContributionToCampaignResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignLedger _ledger;
    private readonly IRewardTierRepository _rewardTierRepository;
    private readonly IRewardTierReservationRepository _rewardTierReservationRepository;
    private readonly ICampaignTransactionExecutor _transactionExecutor;
    private readonly ICampaignRealtimeNotifier _realtimeNotifier;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AddContributionToCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        ICampaignLedger ledger,
        IRewardTierRepository rewardTierRepository,
        IRewardTierReservationRepository rewardTierReservationRepository,
        ICampaignTransactionExecutor transactionExecutor,
        ICampaignRealtimeNotifier realtimeNotifier,
        IDateTimeProvider dateTimeProvider)
    {
        _campaignRepository = campaignRepository;
        _ledger = ledger;
        _rewardTierRepository = rewardTierRepository;
        _rewardTierReservationRepository = rewardTierReservationRepository;
        _transactionExecutor = transactionExecutor;
        _realtimeNotifier = realtimeNotifier;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AddContributionToCampaignResult> Handle(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _transactionExecutor.ExecuteAsync(AdvisoryLockKey.FromGuid(command.CampaignId), async ct =>
        {
            // Loaded inside the lock, so the status seen here cannot change before commit.
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
                           ?? throw new NotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

            if (!campaign.IsAcceptingContributions)
            {
                // Expected, not exceptional: the campaign was cancelled or completed while the
                // payment was in flight. Contributions refunds such a payment itself, so crediting
                // nothing is the correct outcome, and throwing would only retry the event into the
                // dead-letter table. Credited = false reports it.

                return new AddContributionToCampaignResult(campaign.Id, campaign.RaisedAmount.Amount, campaign.RaisedAmount.Currency, Credited: false);
            }

            var entry = campaign.RecordPledge(command.ContributionId, new Money(command.Amount, command.Currency), _dateTimeProvider.UtcNow);
            var append = await _ledger.AppendAsync(entry, ct);

            if (append.Appended && command.RewardTierReservationId.HasValue)
            {
                await ConfirmRewardTierReservationAsync(command.RewardTierReservationId.Value, command.ContributionId, ct);
            }

            return new AddContributionToCampaignResult(campaign.Id, append.LedgerTotal, campaign.GoalAmount.Currency, append.Appended);
        }, cancellationToken);

        if (result.Credited)
        {
            // After commit, and only when the balance changed: a redelivery pushes nothing.
            await _realtimeNotifier.NotifyPledgeReceivedAsync(result.CampaignId, result.RaisedAmount, result.Currency, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Converts the reward tier hold this pledge was checked out against into a permanent claim
    /// — the confirmation half of the Reserve -&gt; Confirm/Expire saga (TICKET-043). Runs nested
    /// inside the campaign's transaction; taking a second <c>pg_advisory_xact_lock</c> keyed by the
    /// tier id is safe (transaction-scoped advisory locks stack) and serializes this confirmation
    /// against a concurrent reservation, release or confirmation on the same tier.
    /// </summary>
    private async Task ConfirmRewardTierReservationAsync(Guid reservationId, Guid contributionId, CancellationToken cancellationToken)
    {
        var reservation = await _rewardTierReservationRepository.GetByIdAsync(reservationId, cancellationToken);

        if (reservation is null || reservation.Status != RewardTierReservationStatus.Reserved)
        {
            // Already expired and released by the scavenger: the pledge itself is still credited.
            return;
        }

        var tierLockKey = AdvisoryLockKey.FromGuid(reservation.RewardTierId);

        await _transactionExecutor.ExecuteAsync(tierLockKey, async ct =>
        {
            var freshReservation = await _rewardTierReservationRepository.GetByIdAsync(reservationId, ct);

            if (freshReservation is null || freshReservation.Status != RewardTierReservationStatus.Reserved)
            {
                return;
            }

            var rewardTier = await _rewardTierRepository.GetByIdAsync(freshReservation.RewardTierId, ct)
                ?? throw new NotFoundException($"Reward tier '{freshReservation.RewardTierId}' was not found.");

            rewardTier.ConfirmClaim();
            freshReservation.Confirm(contributionId);

            await _rewardTierRepository.UpdateAsync(rewardTier, ct);
            await _rewardTierReservationRepository.UpdateAsync(freshReservation, ct);
        }, cancellationToken);
    }
}
