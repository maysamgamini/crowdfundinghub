using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Commands.AddContributionToCampaign;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;

namespace CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Events;

/// <summary>
/// Handles the Contribution Payment Confirmed Application event workflow.
/// </summary>
public sealed class ContributionPaymentConfirmedApplicationEventHandler : IEventHandler<ContributionPaymentConfirmedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public ContributionPaymentConfirmedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    /// <inheritdoc/>
    public async Task Handle(ContributionPaymentConfirmedApplicationEvent notification, CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<AddContributionToCampaignResult>(
            new AddContributionToCampaignCommand(
                notification.CampaignId,
                notification.ContributionId,
                notification.Amount,
                notification.Currency,
                notification.RewardTierReservationId),
            cancellationToken);
    }
}
