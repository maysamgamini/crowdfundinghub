using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignFailed;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignSucceeded;
using CrowdFunding.Modules.Campaigns.Domain.Events;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;

/// <summary>
/// Executes the surrounding module write operation inside a transaction boundary.
/// </summary>
public sealed class CampaignTransactionExecutor : ICampaignTransactionExecutor
{
    private readonly CampaignsDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CampaignTransactionExecutor> _logger;

    // TICKET-037: keys queued by EnqueueCacheInvalidation during the *currently executing*
    // ExecuteAsync call. This executor is a scoped (per-request) service, so there is exactly one
    // logical operation in flight per instance at a time — nested ExecuteAsync calls (advisory
    // lock re-entrancy) share this same list and only the outermost call flushes it, which is
    // exactly the point at which the outermost transaction has committed.
    private readonly List<string> _pendingCacheKeys = [];

    public CampaignTransactionExecutor(CampaignsDbContext dbContext, IDistributedCache cache, ILogger<CampaignTransactionExecutor> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void EnqueueCacheInvalidation(string cacheKey) => _pendingCacheKeys.Add(cacheKey);

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        => ExecuteInternalAsync(null, action, cancellationToken);

    public Task<T> ExecuteAsync<T>(long advisoryLockKey, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
        => ExecuteInternalAsync(advisoryLockKey, action, cancellationToken);

    public Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        => ExecuteInternalAsync<object?>(null, async ct => { await action(ct); return null; }, cancellationToken);

    public Task ExecuteAsync(long advisoryLockKey, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        => ExecuteInternalAsync<object?>(advisoryLockKey, async ct => { await action(ct); return null; }, cancellationToken);

    private async Task<T> ExecuteInternalAsync<T>(
        long? advisoryLockKey,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            if (advisoryLockKey is not null)
            {
                // pg_advisory_xact_lock is transaction-scoped: it blocks other sessions taking
                // the same key until this transaction commits or rolls back, then auto-releases.
                // Serializes the read-modify-write below across concurrent instances of this
                // handler for the same campaign, closing the lost-update / TOCTOU window that
                // xmin alone only detects after the fact.
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({advisoryLockKey.Value})",
                    cancellationToken);
            }

            var result = await action(cancellationToken);
            var domainEvents = DomainEventAccessor.GetDomainEvents(_dbContext);
            var outboxMessages = domainEvents.Select(MapApplicationEvent).ToArray();

            if (outboxMessages.Length > 0)
            {
                await _dbContext.OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            DomainEventAccessor.ClearDomainEvents(_dbContext);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);

                // Only the call that owns the transaction flushes the queue, and only after
                // CommitAsync has actually returned — a concurrent reader can no longer observe
                // the pre-update row by the time this runs, closing the pre-commit eviction
                // window TICKET-037 flags (previously CampaignRepository.UpdateAsync evicted
                // immediately after Update(), before SaveChanges/Commit had even run).
                await FlushPendingCacheInvalidationsAsync(cancellationToken);
            }

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            // Translated to an infrastructure-agnostic exception so application-layer handlers
            // can catch and retry without taking a dependency on Entity Framework Core.
            throw new ConcurrencyConflictException(
                "The aggregate was modified by another transaction. Retry with a fresh read.", ex);
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

                // Clear regardless of commit vs. rollback: on rollback nothing should be evicted
                // (the data didn't actually change), and on commit FlushPendingCacheInvalidationsAsync
                // above already drained the list. Only the owning (outermost) call clears — a
                // nested call must leave the outer call's still-pending keys alone.
                _pendingCacheKeys.Clear();
            }
        }
    }

    private async Task FlushPendingCacheInvalidationsAsync(CancellationToken cancellationToken)
    {
        foreach (var cacheKey in _pendingCacheKeys)
        {
            try
            {
                await _cache.RemoveAsync(cacheKey, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to invalidate cache key {CacheKey} after commit.", cacheKey);
            }
        }
    }

    private static OutboxMessage MapApplicationEvent(BaseEvent domainEvent)
    {
        return domainEvent switch
        {
            CampaignCreatedDomainEvent @event => OutboxMessage.Create(
                new CampaignCreatedApplicationEvent(@event.CampaignId, @event.OwnerId, @event.Title, @event.Currency, @event.DeadlineUtc),
                DateTime.UtcNow),
            CampaignPublishedDomainEvent @event => OutboxMessage.Create(
                new CampaignPublishedApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            CampaignCancelledDomainEvent @event => OutboxMessage.Create(
                new CampaignCancelledApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            CampaignSucceededDomainEvent @event => OutboxMessage.Create(
                new CampaignSucceededApplicationEvent(@event.CampaignId, @event.OwnerId, @event.RaisedAmount, @event.Currency, @event.OccurredOnUtc),
                DateTime.UtcNow),
            CampaignFailedDomainEvent @event => OutboxMessage.Create(
                new CampaignFailedApplicationEvent(@event.CampaignId, @event.OwnerId, @event.RaisedAmount, @event.GoalAmount, @event.Currency, @event.OccurredOnUtc),
                DateTime.UtcNow),
            // Fail loud instead of silently discarding: a domain event raised without a mapping
            // here previously vanished with no log, no error, and no downstream side effect
            // (improvement.md §2.3). Throwing surfaces the gap immediately in tests/CI the
            // moment a new domain event is introduced, rather than silently losing data in prod.
            _ => throw new InvalidOperationException(
                $"No outbox mapping is registered for domain event '{domainEvent.GetType().Name}'.")
        };
    }
}
