using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddPledgeToCampaign;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgePaymentConfirmed;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>
/// Handles the Pledge Payment Confirmed Application event workflow.
/// </summary>
public sealed class PledgePaymentConfirmedApplicationEventHandler : IEventHandler<PledgePaymentConfirmedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public PledgePaymentConfirmedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    /// <inheritdoc/>
    public async Task Handle(PledgePaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<AddPledgeToCampaignResult>(
            new AddPledgeToCampaignCommand(
                notification.CampaignId,
                notification.PledgeId,
                notification.Amount,
                notification.Currency,
                notification.RewardTierReservationId),
            cancellationToken);
    }
}
