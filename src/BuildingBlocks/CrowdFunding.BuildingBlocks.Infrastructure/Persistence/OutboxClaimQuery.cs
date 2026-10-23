using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Atomically claims a batch of Pending outbox rows using PostgreSQL's <c>FOR UPDATE SKIP
/// LOCKED</c>, so multiple instances of the background processor polling the same table
/// concurrently each walk away with a disjoint batch instead of racing on the same rows (the
/// multi-instance duplicate-dispatch defect in improvement.md §2.2). See
/// docs/outbox-architecture.md for the full design rationale versus Debezium CDC.
/// </summary>
public static class OutboxClaimQuery
{
    public static Task<List<OutboxMessage>> ClaimPendingBatchAsync(
        this DbContext dbContext,
        string tableName,
        string workerId,
        int batchSize,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        // tableName is always one of this project's hardcoded "*_outbox_messages" constants
        // (never user input), so splicing it into the SQL text is safe — it cannot be passed as
        // a normal query parameter because parameters bind values, not identifiers. The claim
        // and the Pending -> Processing transition happen in one statement (CTE + UPDATE ...
        // RETURNING) so there is no window between "select candidate rows" and "mark them
        // claimed" for a second worker to slip into.
        // "Due" is judged on the application clock, the same clock that wrote ScheduledAtUtc. Judging
        // it on the database's now() made a row look as if it were scheduled in the future whenever
        // the database host's clock ran even a few milliseconds behind the application's (as a
        // container's does), so a batch started right after an event was queued could skip it.
        // The module's schema comes from the model, so the statement hits the right table
        // whatever schema the module's DbContext maps to.
        var schema = dbContext.Model.FindEntityType(typeof(OutboxMessage))?.GetSchema();
        var qualifiedTable = schema is null ? tableName : $"\"{schema}\".{tableName}";

        var sql = $$"""
            WITH claimable AS (
                SELECT "Id"
                FROM {{qualifiedTable}}
                WHERE "Status" = {{(int)OutboxMessageStatus.Pending}}
                  AND "ScheduledAtUtc" <= {3}
                ORDER BY "ScheduledAtUtc", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {0}
            )
            UPDATE {{qualifiedTable}} m
            SET "Status" = {{(int)OutboxMessageStatus.Processing}},
                "LockedBy" = {1},
                "LockedUntilUtc" = now() + ({2} || ' milliseconds')::interval
            FROM claimable
            WHERE m."Id" = claimable."Id"
            RETURNING m.*;
            """;

        return dbContext.Set<OutboxMessage>()
            .FromSqlRaw(sql, batchSize, workerId, (int)lockDuration.TotalMilliseconds, DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }
}
