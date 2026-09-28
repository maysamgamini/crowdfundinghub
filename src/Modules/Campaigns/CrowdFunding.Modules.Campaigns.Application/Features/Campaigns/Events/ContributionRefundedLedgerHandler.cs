using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Commands.RecordContributionRefund;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>
/// Reverses a refunded contribution in the campaign's ledger. Idempotent by construction: the
/// ledger's unique (ContributionId, Kind) key turns a redelivered event into a no-op.
/// </summary>
public sealed class ContributionRefundedLedgerHandler : IEventHandler<ContributionRefundedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContributionRefundedLedgerHandler"/> class.
    /// </summary>
    /// <param name="commandDispatcher">The command dispatcher.</param>
    public ContributionRefundedLedgerHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    /// <inheritdoc/>
    public Task Handle(ContributionRefundedApplicationEvent notification, CancellationToken cancellationToken)
        => _commandDispatcher.SendAsync<RecordContributionRefundResult>(
            new RecordContributionRefundCommand(notification.CampaignId, notification.ContributionId, notification.Amount, notification.Currency),
            cancellationToken);
}
