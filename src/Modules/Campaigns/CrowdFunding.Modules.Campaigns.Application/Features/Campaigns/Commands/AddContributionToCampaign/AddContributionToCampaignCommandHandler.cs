using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
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

    public AddContributionToCampaignCommandHandler(
        ICampaignRepository campaignRepository,
        IContributionLedger contributionLedger,
        ICampaignTransactionExecutor transactionExecutor)
    {
        _campaignRepository = campaignRepository;
        _contributionLedger = contributionLedger;
        _transactionExecutor = transactionExecutor;
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
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, cancellationToken);

            if (campaign is null)
            {
                throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
            }

            try
            {
                await _transactionExecutor.ExecuteAsync(advisoryLockKey, async ct =>
                {
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

                    return 0;
                }, cancellationToken);

                return new AddContributionToCampaignResult(campaign.Id, campaign.RaisedAmount.Amount, campaign.RaisedAmount.Currency);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxConcurrencyRetries)
            {
                // Another writer's xmin token won the race despite the advisory lock (e.g. a
                // concurrent update outside this lock's coverage). Re-fetch and retry with
                // jittered backoff rather than surfacing a transient conflict as a failure.
                var delay = RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 25));
                await Task.Delay(delay + jitter, cancellationToken);
            }
        }
    }
}
