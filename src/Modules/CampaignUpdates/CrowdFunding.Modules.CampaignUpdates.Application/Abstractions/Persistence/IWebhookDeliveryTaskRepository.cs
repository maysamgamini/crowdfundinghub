using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

public interface IWebhookDeliveryTaskRepository
{
    Task AddAsync(WebhookDeliveryTask task, CancellationToken cancellationToken);

    /// <summary>Claims every due (<c>Pending</c>, <c>ScheduledAtUtc &lt;= now</c>) task, up to
    /// <paramref name="batchSize"/> — the dispatcher's poll loop.</summary>
    Task<IReadOnlyList<WebhookDeliveryTask>> ClaimDueBatchAsync(int batchSize, DateTime nowUtc, CancellationToken cancellationToken);

    Task UpdateAsync(WebhookDeliveryTask task, CancellationToken cancellationToken);
}
