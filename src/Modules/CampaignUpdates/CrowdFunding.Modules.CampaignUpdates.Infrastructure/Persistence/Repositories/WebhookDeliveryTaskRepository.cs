using CrowdFunding.Modules.CampaignUpdates.Application.Abstractions.Persistence;
using CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;
using CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.Modules.CampaignUpdates.Infrastructure.Persistence.Repositories;

public sealed class WebhookDeliveryTaskRepository : IWebhookDeliveryTaskRepository
{
    private readonly CampaignUpdatesDbContext _dbContext;

    public WebhookDeliveryTaskRepository(CampaignUpdatesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(WebhookDeliveryTask task, CancellationToken cancellationToken)
    {
        _dbContext.WebhookDeliveryTasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookDeliveryTask>> ClaimDueBatchAsync(
        int batchSize,
        DateTime nowUtc,
        string workerId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        // Atomically claims the batch: the SELECT ... FOR UPDATE SKIP LOCKED and the
        // Pending/Processing -> Processing transition happen in one statement (CTE + UPDATE ...
        // RETURNING), so there is no window between "select candidate rows" and "mark them
        // claimed" for a second dispatcher replica to slip into and pick up the same rows. Also
        // reclaims Processing rows whose lock has expired (e.g. the worker that claimed them
        // crashed mid-delivery), mirroring OutboxClaimQuery's lock-lease pattern.
        var lockedUntilUtc = nowUtc.Add(lockDuration);

        const string sql = """
            WITH claimable AS (
                SELECT "Id"
                FROM webhook_delivery_tasks
                WHERE ("Status" = 'Pending' AND "ScheduledAtUtc" <= {0})
                   OR ("Status" = 'Processing' AND "LockedUntilUtc" IS NOT NULL AND "LockedUntilUtc" <= {0})
                ORDER BY "ScheduledAtUtc"
                FOR UPDATE SKIP LOCKED
                LIMIT {1}
            )
            UPDATE webhook_delivery_tasks t
            SET "Status" = 'Processing',
                "WorkerId" = {2},
                "LockedUntilUtc" = {3}
            FROM claimable
            WHERE t."Id" = claimable."Id"
            RETURNING t.*;
            """;

        return await _dbContext.WebhookDeliveryTasks
            .FromSqlRaw(sql, nowUtc, batchSize, workerId, lockedUntilUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(WebhookDeliveryTask task, CancellationToken cancellationToken)
    {
        _dbContext.WebhookDeliveryTasks.Update(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
