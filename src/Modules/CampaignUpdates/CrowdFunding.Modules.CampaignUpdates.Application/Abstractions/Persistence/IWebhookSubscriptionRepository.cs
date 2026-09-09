using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

public interface IWebhookSubscriptionRepository
{
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    Task<WebhookSubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookSubscription>> GetActiveByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);

    Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken);
}
