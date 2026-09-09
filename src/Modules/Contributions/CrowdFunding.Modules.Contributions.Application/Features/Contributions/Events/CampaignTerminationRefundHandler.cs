using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Events;

/// <summary>
/// The refund compensation saga (event-driven choreography, no distributed 2PC transaction):
/// when Campaigns marks a campaign Failed or Cancelled and publishes that as an application
/// event via its own outbox, Contributions reacts asynchronously by refunding every backer who
/// had already had their payment confirmed — Campaigns never opens a transaction against
/// Contributions' database to do this itself.
///
/// Naturally idempotent: <see cref="IContributionRepository.GetSucceededByCampaignIdAsync"/> only
/// ever returns contributions still in <c>Succeeded</c> status, so re-delivering either
/// triggering event a second time finds nothing left to refund and is a safe no-op.
/// </summary>
public sealed class CampaignTerminationRefundHandler :
    IEventHandler<CampaignFailedApplicationEvent>,
    IEventHandler<CampaignCancelledApplicationEvent>
{
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;
    private readonly IContributionDateTimeProvider _dateTimeProvider;

    public CampaignTerminationRefundHandler(
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor,
        IContributionDateTimeProvider dateTimeProvider)
    {
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task Handle(CampaignFailedApplicationEvent notification, CancellationToken cancellationToken)
        => RefundAllSucceededContributionsAsync(notification.CampaignId, cancellationToken);

    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken)
        => RefundAllSucceededContributionsAsync(notification.CampaignId, cancellationToken);

    private async Task RefundAllSucceededContributionsAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        const int batchSize = 100;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _contributionRepository.GetSucceededBatchByCampaignIdAsync(campaignId, batchSize, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                foreach (var contribution in batch)
                {
                    contribution.Refund(_dateTimeProvider.UtcNow);
                    await _contributionRepository.UpdateAsync(contribution, ct);
                }
            }, cancellationToken);
        }
    }
}
