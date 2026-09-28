using CrowdFunding.BuildingBlocks.Application.Inbox;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Inbox;

/// <summary>
/// EF Core implementation of <see cref="IIdempotentConsumer"/> over one module's DbContext and its
/// processed-events table (mapped with <c>ConfigureInbox</c>).
/// </summary>
/// <remarks>
/// <para>
/// The claim is a single <c>INSERT … ON CONFLICT DO NOTHING</c> executed before the effect, in
/// the same transaction. There is no check-then-insert window: when two deliveries of the same
/// message run concurrently, the second insert waits on the first transaction's uncommitted key,
/// then either inserts nothing (the first committed, so the effect is skipped) or proceeds (the
/// first rolled back, so the effect was never applied).
/// </para>
/// <para>
/// Joins the caller's transaction when one is already open (for example inside a module
/// transaction executor), otherwise opens and commits its own. A failing effect rolls back the
/// claim too, so the message is retried rather than lost.
/// </para>
/// </remarks>
/// <typeparam name="TDbContext">The module DbContext that owns the processed-events table.</typeparam>
public abstract class EfIdempotentConsumer<TDbContext> : IIdempotentConsumer
    where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfIdempotentConsumer{TDbContext}"/> class.
    /// </summary>
    /// <param name="dbContext">The module DbContext.</param>
    protected EfIdempotentConsumer(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteOnceAsync(
        string eventId,
        string consumerName,
        Func<CancellationToken, Task> effect,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentNullException.ThrowIfNull(effect);

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (!await TryClaimAsync(eventId, consumerName, cancellationToken))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return false;
            }

            await effect(cancellationToken);

            if (transaction is not null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> TryClaimAsync(string eventId, string consumerName, CancellationToken cancellationToken)
    {
        // The schema-qualified table name comes from the EF model (ConfigureInbox inside the
        // module's own schema), never from input, so interpolating the identifier is safe; the
        // values are bound as parameters.
        var qualifiedTable = QualifiedTableName.Of<ProcessedEvent>(_dbContext);

        var sql = $$"""
            INSERT INTO {{qualifiedTable}} ("{{nameof(ProcessedEvent.EventId)}}", "{{nameof(ProcessedEvent.ConsumerName)}}", "{{nameof(ProcessedEvent.ProcessedAtUtc)}}")
            VALUES ({0}, {1}, now())
            ON CONFLICT DO NOTHING
            """;

        var inserted = await _dbContext.Database.ExecuteSqlRawAsync(
            sql,
            [eventId, consumerName],
            cancellationToken);

        return inserted == 1;
    }
}
