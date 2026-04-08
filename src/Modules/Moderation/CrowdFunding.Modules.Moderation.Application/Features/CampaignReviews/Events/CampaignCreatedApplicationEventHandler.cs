using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Messaging;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Commands.CreateCampaignReview;

namespace CrowdFunding.Modules.Moderation.Application.Features.CampaignReviews.Events;

public sealed class CampaignCreatedApplicationEventHandler : IEventHandler<CampaignCreatedApplicationEvent>
{
    private readonly ICommandDispatcher _commandDispatcher;

    public CampaignCreatedApplicationEventHandler(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    public async Task Handle(CampaignCreatedApplicationEvent notification, CancellationToken cancellationToken)
    {
        await _commandDispatcher.SendAsync<CreateCampaignReviewResult>(
            new CreateCampaignReviewCommand(notification.CampaignId),
            cancellationToken);
    }
}