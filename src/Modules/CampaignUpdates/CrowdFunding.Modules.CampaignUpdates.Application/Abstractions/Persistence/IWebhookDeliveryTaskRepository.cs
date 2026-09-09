using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

namespace CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;

public interface IWebhookDeliveryTaskRepository
{
    Task AddAsync(WebhookDeliveryTask task, CancellationToken cancellationToken);

    /// <summary>Atomically claims every due (<c>Pending</c>, <c>ScheduledAtUtc &lt;= now</c>,
    /// plus any <c>Processing</c> row whose lock has expired) task, up to
    /// <paramref name="batchSize"/> — the dispatcher's poll loop. Uses <c>FOR UPDATE SKIP
    /// LOCKED</c> so multiple dispatcher replicas polling concurrently each walk away with a
    /// disjoint batch instead of double-dispatching the same webhook (TICKET-046).</summary>
    Task<IReadOnlyList<WebhookDeliveryTask>> ClaimDueBatchAsync(
        int batchSize,
        DateTime nowUtc,
        string workerId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken);

    Task UpdateAsync(WebhookDeliveryTask task, CancellationToken cancellationToken);
}
