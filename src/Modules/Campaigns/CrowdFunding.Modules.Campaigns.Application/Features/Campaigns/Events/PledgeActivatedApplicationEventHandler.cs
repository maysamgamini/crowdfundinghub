using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddPledgeToCampaign;
using CrowdFunding.Modules.Pledges.Contracts.Events.PledgeActivated;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>
/// A pledge's card was saved, so the pledge counts toward the campaign. In the save-then-charge funding model the
/// promise is what counts at the deadline, not the money (that is only taken after the campaign is funded).
/// </summary>
public sealed class PledgeActivatedApplicationEventHandler : IEventHandler<PledgeActivatedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public PledgeActivatedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    public async Task Handle(PledgeActivatedApplicationEvent notification, CancellationToken cancellationToken)
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
