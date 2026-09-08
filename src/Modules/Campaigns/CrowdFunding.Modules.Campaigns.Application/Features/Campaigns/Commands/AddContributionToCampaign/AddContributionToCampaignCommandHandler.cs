using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.AddContributionToCampaign;

/// <summary>
/// Handles Add Contribution To Campaign command requests.
/// </summary>
public sealed class AddContributionToCampaignCommandHandler : ICommandHandler<AddContributionToCampaignCommand, AddContributionToCampaignResult>
{
    private const int MaxConcurrencyRetries = 3;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(20),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(120),
    ];

    private readonly ICampaignRepository _campaignRepository;
    private readonly IContributionLedger _contributionLedger;
    private readonly ICampaignTransactionExecutor _transactionExecutor;
    private readonly ICampaignRealtimeNotifier _realtimeNotifier;

    public AddContributionToCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IContributionLedger contributionLedger,
        ICampaignTransactionExecutor transactionExecutor,
        ICampaignRealtimeNotifier realtimeNotifier)
    {
        _campaignRepository = campaignRepository;
        _contributionLedger = contributionLedger;
        _transactionExecutor = transactionExecutor;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<AddContributionToCampaignResult> Handle(
        AddContributionToCampaignCommand command,
        CancellationToken cancellationToken)
    {
        // Derive a deterministic 64-bit key from the campaign id so every writer targeting the
        // same campaign contends for the same pg_advisory_xact_lock, serializing the
        // read-modify-write below across concurrent instances/requests.
        var advisoryLockKey = unchecked((long)command.CampaignId.GetHashCode());

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var campaignId = Guid.Empty;
                var raisedAmount = 0m;
                var raisedCurrency = string.Empty;
                var wasRecorded = false;

                await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
                {
                    // The campaign is (re-)loaded *inside* the locked critical section, not
                    // before it. Loading it before waiting on pg_advisory_xact_lock would let a
                    // queued writer hold a snapshot that is already stale by the time its turn
                    // comes, defeating the lock's purpose and starving the xmin retry loop under
                    // real contention (reproduced against real PostgreSQL: 20 concurrent writers
                    // exhausted 3 retries when the read happened before the lock).
                    var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct);

                    if (campaign is null)
                    {
                        throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
                    }

                    // Idempotency guard: at-least-once delivery of the payment-confirmed event
                    // must not double-credit the campaign. If this contribution was already
                    // recorded, treat the retry as a no-op instead of re-applying the balance.
                    var recorded = await _contributionLedger.TryRecordAsync(
                        command.CampaignId,
                        command.ContributionId,
                        command.Amount,
                        command.Currency,
                        ct);

                    if (recorded)
                    {
                        campaign.ApplyConfirmedContribution(new Money(command.Amount, command.Currency));
                        await _campaignRepository.UpdateAsync(campaign, ct);
                    }

                    campaignId = campaign.Id;
                    raisedAmount = campaign.RaisedAmount.Amount;
                    raisedCurrency = campaign.RaisedAmount.Currency;
                    wasRecorded = recorded;

                    return 0;
                }, cancellationToken);

                if (wasRecorded)
                {
                    // Only broadcast when the balance actually changed — redelivery of an
                    // already-recorded contribution is a no-op and would otherwise push a
                    // duplicate, stale-looking update to connected clients.
                    await _realtimeNotifier.NotifyPledgeReceivedAsync(campaignId, raisedAmount, raisedCurrency, cancellationToken);
                }

                return new AddContributionToCampaignResult(campaignId, raisedAmount, raisedCurrency);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxConcurrencyRetries)
            {
                // Defense-in-depth: should be rare now that the read happens inside the lock,
                // but a writer that bypasses this executor (e.g. a future direct migration
                // script) could still race the xmin token. Retry with jittered backoff rather
                // than surfacing a transient conflict as a failure.
                var delay = RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 25));
                await Task.Delay(delay + jitter, cancellationToken);
            }
        }
    }
}
