using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Domain.ValueObjects;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.RecordContributionRefund;

/// <summary>
/// Handles <see cref="RecordContributionRefundCommand"/>: a refund is a new, negative ledger entry,
/// never an update or deletion of the pledge it reverses.
/// </summary>
/// <remarks>
/// The database appends it only if a pledge entry exists for the contribution and no refund entry
/// does yet, so a refund of a contribution that was never credited (the campaign was already
/// cancelled when the payment confirmed) changes nothing, and a redelivered refund is a no-op.
/// </remarks>
public sealed class RecordContributionRefundCommandHandler : ICommandHandler<RecordContributionRefundCommand, RecordContributionRefundResult>
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignLedger _ledger;
    private readonly ICampaignTransactionExecutor _transactionExecutor;
    private readonly ICampaignRealtimeNotifier _realtimeNotifier;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RecordContributionRefundCommandHandler(
        ICampaignRepository campaignRepository,
        ICampaignLedger ledger,
        ICampaignTransactionExecutor transactionExecutor,
        ICampaignRealtimeNotifier realtimeNotifier,
        IDateTimeProvider dateTimeProvider)
    {
        _campaignRepository = campaignRepository;
        _ledger = ledger;
        _transactionExecutor = transactionExecutor;
        _realtimeNotifier = realtimeNotifier;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RecordContributionRefundResult> Handle(RecordContributionRefundCommand command, CancellationToken cancellationToken)
    {
        var currency = string.Empty;

        var result = await _transactionExecutor.ExecuteAsync(AdvisoryLockKey.FromGuid(command.CampaignId), async ct =>
        {
            var campaign = await _campaignRepository.GetByIdAsync(command.CampaignId, ct)
                           ?? throw new NotFoundException($"Campaign with id '{command.CampaignId}' was not found.");

            var entry = campaign.RecordRefund(command.ContributionId, new Money(command.Amount, command.Currency), _dateTimeProvider.UtcNow);
            var append = await _ledger.AppendAsync(entry, ct);
            currency = campaign.GoalAmount.Currency;

            return new RecordContributionRefundResult(campaign.Id, append.LedgerTotal, append.Appended);
        }, cancellationToken);

        if (result.Reversed)
        {
            await _realtimeNotifier.NotifyPledgeReceivedAsync(result.CampaignId, result.RaisedAmount, currency, cancellationToken);
        }

        return result;
    }
}
