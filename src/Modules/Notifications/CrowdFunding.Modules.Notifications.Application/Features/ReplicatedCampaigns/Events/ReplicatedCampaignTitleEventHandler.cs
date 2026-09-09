using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;

namespace CrowdFunding.Modules.Notifications.Application.Features.ReplicatedCampaigns.Events;

/// <summary>
/// Replicates a campaign's title and owner into Notifications' own local
/// <see cref="ICampaignTitleCacheRepository"/> (Event-Carried State Transfer), so outbound email
/// notifications can name the campaign without a synchronous cross-module call. See TICKET-031 /
/// TICKET-023.
/// </summary>
public sealed class ReplicatedCampaignTitleEventHandler : IEventHandler<CampaignCreatedApplicationEvent>
{
    private readonly ICampaignTitleCacheRepository _repository;

    public ReplicatedCampaignTitleEventHandler(ICampaignTitleCacheRepository repository)
    {
        _repository = repository;
    }

    public Task Handle(CampaignCreatedApplicationEvent notification, CancellationToken cancellationToken)
        => _repository.UpsertAsync(notification.CampaignId, notification.Title, notification.OwnerId, DateTime.UtcNow, cancellationToken);
}
