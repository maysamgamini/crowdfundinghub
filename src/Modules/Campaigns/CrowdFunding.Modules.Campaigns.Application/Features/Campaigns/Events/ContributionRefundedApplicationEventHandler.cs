using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.ReverseContributionOnCampaign;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionRefunded;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>A contribution was refunded in Contributions: book the compensating entry against the campaign.</summary>
public sealed class ContributionRefundedApplicationEventHandler : IEventHandler<ContributionRefundedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public ContributionRefundedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    public async Task Handle(ContributionRefundedApplicationEvent notification, CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<ReverseContributionOnCampaignResult>(
            new ReverseContributionOnCampaignCommand(
                notification.CampaignId,
                notification.ContributionId,
                notification.Amount,
                notification.Currency),
            cancellationToken);
    }
}
