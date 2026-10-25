using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Events;

/// <summary>
/// The refund compensation saga (event-driven choreography, no distributed 2PC transaction): when Campaigns marks a
/// campaign Failed or Cancelled and publishes that as an application event through its own outbox, Pledges opens a
/// refund job for the campaign. The job, not this handler, refunds the pledges (see <see cref="RefundJobService"/>): a
/// handler that refunded everything inside one event delivery could not be rate limited, could not be resumed in small
/// steps, and would hold the delivery for as long as the provider took.
///
/// Naturally idempotent: a second delivery finds the job and does nothing, and the unique index on the campaign
/// refuses a second job if two deliveries race.
/// </summary>
public sealed class CampaignTerminationRefundHandler :
    IEventHandler<CampaignFailedApplicationEvent>,
    IEventHandler<CampaignCancelledApplicationEvent>,
    IRecordsInboxInTransaction
{
    private readonly IRefundStore _refundStore;
    private readonly IPledgeTransactionExecutor _transactionExecutor;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;

    public CampaignTerminationRefundHandler(
        IRefundStore refundStore,
        IPledgeTransactionExecutor transactionExecutor,
        IPledgeDateTimeProvider dateTimeProvider)
    {
        _refundStore = refundStore;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task Handle(CampaignFailedApplicationEvent notification, CancellationToken cancellationToken)
        => OpenJobAsync(notification.CampaignId, cancellationToken);

    public Task Handle(CampaignCancelledApplicationEvent notification, CancellationToken cancellationToken)
        => OpenJobAsync(notification.CampaignId, cancellationToken);

    private async Task OpenJobAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        if (await _refundStore.JobExistsForCampaignAsync(campaignId, cancellationToken))
        {
            return;
        }

        try
        {
            await _transactionExecutor.ExecuteAsync(
                ct => _refundStore.AddJobAsync(RefundJob.Create(campaignId, _dateTimeProvider.UtcNow), ct),
                cancellationToken);
        }
        catch (RefundJobAlreadyExistsException)
        {
            // Two deliveries raced past the check above; the unique index let one through. That one is enough.
        }
    }
}
