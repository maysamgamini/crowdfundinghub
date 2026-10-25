using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.ReversePledgeOnCampaign;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeRefunded;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>A pledge was refunded in Pledges: book the compensating entry against the campaign.</summary>
public sealed class PledgeRefundedApplicationEventHandler : IEventHandler<PledgeRefundedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public PledgeRefundedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    public async Task Handle(PledgeRefundedApplicationEvent notification, CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<ReversePledgeOnCampaignResult>(
            new ReversePledgeOnCampaignCommand(
                notification.CampaignId,
                notification.PledgeId,
                notification.Amount,
                notification.Currency),
            cancellationToken);
    }
}
