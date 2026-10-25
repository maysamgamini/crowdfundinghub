using CrowdFunding.BuildingBlocks.Application.Events;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// <see cref="IInbox"/> backed by the inbox table of one module's DbContext.
/// </summary>
public sealed class EfInbox<TDbContext> : IInbox
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    public EfInbox(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken)
        => _dbContext.Set<InboxMessage>()
            .AsNoTracking()
            .AnyAsync(x => x.MessageId == messageId && x.Consumer == consumer, cancellationToken);

    public async Task MarkProcessedAsync(Guid messageId, string consumer, DateTime processedAtUtc, CancellationToken cancellationToken)
        => await TryRecordAsync(messageId, consumer, processedAtUtc, cancellationToken);

    /// <summary>
    /// Inserts the inbox row on this DbContext's connection, so inside its current transaction when
    /// there is one. Returns false when the row already existed.
    /// </summary>
    public async Task<bool> TryRecordAsync(Guid messageId, string consumer, DateTime processedAtUtc, CancellationToken cancellationToken)
    {
        // A direct INSERT rather than Add + SaveChanges: SaveChanges would also flush whatever
        // else the handler left tracked on this DbContext. ON CONFLICT makes a repeat harmless.
        var entityType = _dbContext.Model.FindEntityType(typeof(InboxMessage))!;
        var table = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var qualified = schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";

#pragma warning disable EF1002 // identifiers come from the EF model, never from user input
        var inserted = await _dbContext.Database.ExecuteSqlRawAsync(
            $$"""
             INSERT INTO {{qualified}} ("MessageId", "Consumer", "ProcessedAtUtc")
             VALUES ({0}, {1}, {2})
             ON CONFLICT ("MessageId", "Consumer") DO NOTHING
             """,
            [messageId, consumer, processedAtUtc],
            cancellationToken);
#pragma warning restore EF1002

        return inserted > 0;
    }
}
