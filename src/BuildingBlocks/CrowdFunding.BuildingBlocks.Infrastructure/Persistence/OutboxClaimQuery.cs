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
        string workerId,
        int batchSize,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        // The table name is read from the EF model, schema-qualified (each module's outbox lives
        // in that module's schema), so it can never drift from the mapping and never comes from
        // user input; splicing it into the SQL text is therefore safe. It cannot be passed as a
        // normal query parameter because parameters bind values, not identifiers. The claim
        // and the Pending -> Processing transition happen in one statement (CTE + UPDATE ...
        // RETURNING) so there is no window between "select candidate rows" and "mark them
        // claimed" for a second worker to slip into.
        //
        // Lease recovery: the claim commits on its own, before publishing, so a worker that
        // crashes mid-batch leaves its rows in Processing. Once LockedUntilUtc has passed, those
        // rows are claimable again and the abandoned pass counts as an attempt (so a message that
        // crashes the process every time still reaches MaxAttempts and the dead-letter table).
        // Consumers tolerate the resulting redelivery through IIdempotentConsumer.
        var tableName = QualifiedTableName.Of<OutboxMessage>(dbContext);

        var sql = $$"""
            WITH claimable AS (
                SELECT "Id"
                FROM {{tableName}}
                WHERE ("Status" = {{(int)OutboxMessageStatus.Pending}} AND "ScheduledAtUtc" <= now())
                   OR ("Status" = {{(int)OutboxMessageStatus.Processing}} AND "LockedUntilUtc" < now())
                ORDER BY "ScheduledAtUtc", "Id"
                FOR UPDATE SKIP LOCKED
                LIMIT {0}
            )
            UPDATE {{tableName}} m
            SET "Attempts" = m."Attempts" + CASE WHEN m."Status" = {{(int)OutboxMessageStatus.Processing}} THEN 1 ELSE 0 END,
                "Status" = {{(int)OutboxMessageStatus.Processing}},
                "LockedBy" = {1},
                "LockedUntilUtc" = now() + ({2} || ' milliseconds')::interval
            FROM claimable
            WHERE m."Id" = claimable."Id"
            RETURNING m.*;
            """;

        return dbContext.Set<OutboxMessage>()
            .FromSqlRaw(sql, batchSize, workerId, (int)lockDuration.TotalMilliseconds)
            .ToListAsync(cancellationToken);
    }
}
