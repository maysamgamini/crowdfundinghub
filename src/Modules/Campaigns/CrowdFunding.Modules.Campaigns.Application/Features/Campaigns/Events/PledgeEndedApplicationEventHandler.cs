using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.ReversePledgeOnCampaign;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentFailed;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeReleased;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>
/// A pledge that was counted will not be collected: it was released because the campaign did not get funded, or its
/// charge failed for good. The campaign books a compensating entry so that the ledger keeps matching what is really
/// owed. A pledge that was never counted (its card was never saved) makes this a harmless no-op.
/// </summary>
public sealed class PledgeEndedApplicationEventHandler :
    IEventHandler<PledgeReleasedApplicationEvent>,
    IEventHandler<PledgePaymentFailedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public PledgeEndedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    public Task Handle(PledgeReleasedApplicationEvent notification, CancellationToken cancellationToken)
        => ReverseAsync(notification.CampaignId, notification.PledgeId, notification.Amount, notification.Currency, cancellationToken);

    public Task Handle(PledgePaymentFailedApplicationEvent notification, CancellationToken cancellationToken)
        => ReverseAsync(notification.CampaignId, notification.PledgeId, notification.Amount, notification.Currency, cancellationToken);

    private async Task ReverseAsync(Guid campaignId, Guid pledgeId, decimal amount, string currency, CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<ReversePledgeOnCampaignResult>(
            new ReversePledgeOnCampaignCommand(campaignId, pledgeId, amount, currency),
            cancellationToken);
    }
}
