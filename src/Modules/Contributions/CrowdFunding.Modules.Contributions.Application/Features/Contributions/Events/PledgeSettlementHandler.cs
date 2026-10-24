using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignSucceeded;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Services;
using CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Contributions.Domain.Enums;

namespace CrowdFunding.Modules.Contributions.Application.Features.Contributions.Events;

/// <summary>
/// What happens to the pledges of a campaign when it ends, in the save-then-charge funding model. A funded
/// campaign puts its active pledges in the queue to be charged. A campaign that failed or was cancelled releases its
/// pledges: nothing was ever charged, so there is nothing to refund and no provider call to make.
/// </summary>
public sealed class PledgeSettlementHandler :
    IEventHandler<CampaignSucceededApplicationEvent>,
    IEventHandler<CampaignFailedApplicationEvent>,
    IEventHandler<CampaignCancelledApplicationEvent>
{
    private const int BatchSize = 100;

    private readonly IPledgeSettlementStore _store;
    private readonly IContributionRepository _contributionRepository;
    private readonly IContributionTransactionExecutor _transactionExecutor;
    private readonly IContributionDateTimeProvider _dateTimeProvider;
    private readonly IPaymentSettings _settings;

    public PledgeSettlementHandler(
        IPledgeSettlementStore store,
        IContributionRepository contributionRepository,
        IContributionTransactionExecutor transactionExecutor,
        IContributionDateTimeProvider dateTimeProvider,
        IPaymentSettings settings)
    {
        _store = store;
        _contributionRepository = contributionRepository;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
    }

    public async Task Handle(CampaignSucceededApplicationEvent notification, CancellationToken cancellationToken)
    {
        if (!AppliesToThisFundingModel())
        {
            return;
        }

        // Pledges whose card is saved wait in the queue to be charged.
        await ForEachBatchAsync(
            notification.CampaignId,
            [ContributionStatus.Active],
            (pledge, now) => pledge.QueueForCharge(now),
            cancellationToken);

        // Pledges that never finished saving their card never counted and cannot be charged: they expire.
        await ReleaseAsync(notification.CampaignId, [ContributionStatus.Pending], cancellationToken);
    }

    public Task Handle(CampaignFailedApplicationEvent notification, CancellationToken cancellationToken)
        => ReleaseAsync(notification.CampaignId, [ContributionStatus.Pending, ContributionStatus.Active], cancellationToken);

    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken)
        => ReleaseAsync(notification.CampaignId, [ContributionStatus.Pending, ContributionStatus.Active], cancellationToken);

    // In the charge-now model there is nothing to queue and nothing to release: money was taken at confirmation and
    // the refund handler takes it back. This handler leaves such campaigns alone.
    private bool AppliesToThisFundingModel() => _settings.FundingModel == FundingModel.SaveThenCharge;

    private Task ReleaseAsync(Guid campaignId, IReadOnlyCollection<ContributionStatus> statuses, CancellationToken cancellationToken)
        => AppliesToThisFundingModel()
            ? ForEachBatchAsync(campaignId, statuses, (pledge, now) => pledge.ReleasePledge(now), cancellationToken)
            : Task.CompletedTask;

    private async Task ForEachBatchAsync(
        Guid campaignId,
        IReadOnlyCollection<ContributionStatus> statuses,
        Action<Domain.Aggregates.Contribution, DateTime> change,
        CancellationToken cancellationToken)
    {
        // A pledge that is no longer in one of these statuses after its change drops out of the next query, so a
        // redelivered event finds nothing left to do and the loop ends.
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _store.GetBatchAsync(campaignId, statuses, BatchSize, cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            await _transactionExecutor.ExecuteAsync(async ct =>
            {
                foreach (var pledge in batch)
                {
                    change(pledge, _dateTimeProvider.UtcNow);
                    await _contributionRepository.UpdateAsync(pledge, ct);
                }
            }, cancellationToken);
        }
    }
}
