using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

public interface IWebhookSubscriptionRepository
{
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    Task<WebhookSubscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookSubscription>> GetActiveByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);

    /// <summary>Every subscription for a campaign regardless of <c>IsActive</c> — the creator's
    /// management view (list/GET-by-id/DELETE), as opposed to <see cref="GetActiveByCampaignIdAsync"/>
    /// which only the dispatcher's send loop uses.</summary>
    Task<IReadOnlyList<WebhookSubscription>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken);

    Task UpdateAsync(WebhookSubscription subscription, CancellationToken cancellationToken);
}
