using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignSucceeded;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Services;
using CrowdFunding.Modules.Pledges.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Pledges.Domain.Aggregates;

namespace CrowdFunding.Modules.Pledges.Application.Features.Pledges.Events;

/// <summary>
/// When a campaign succeeds, records that its creator is owed a payout. Nothing is paid here: the amount is not
/// known until every pledge has settled, so <see cref="PayoutService"/> does that later. The same event arriving
/// twice finds the payout already there and does nothing.
/// </summary>
public sealed class PayoutCreationHandler : IEventHandler<CampaignSucceededApplicationEvent>
{
    private readonly IPayoutStore _payoutStore;
    private readonly IPledgeTransactionExecutor _transactionExecutor;
    private readonly IPledgeDateTimeProvider _dateTimeProvider;

    public PayoutCreationHandler(
        IPayoutStore payoutStore,
        IPledgeTransactionExecutor transactionExecutor,
        IPledgeDateTimeProvider dateTimeProvider)
    {
        _payoutStore = payoutStore;
        _transactionExecutor = transactionExecutor;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task Handle(CampaignSucceededApplicationEvent notification, CancellationToken cancellationToken)
    {
        if (await _payoutStore.ExistsForCampaignAsync(notification.CampaignId, cancellationToken))
        {
            return;
        }

        try
        {
            await _transactionExecutor.ExecuteAsync(
                ct => _payoutStore.AddAsync(
                    Payout.Create(notification.CampaignId, notification.OwnerId, notification.Currency, _dateTimeProvider.UtcNow),
                    ct),
                cancellationToken);
        }
        catch (PayoutAlreadyExistsException)
        {
            // Two deliveries raced past the check above; the unique index let one through. That one is enough.
        }
    }
}
