using CrowdFunding.BuildingBlocks.Application.Exceptions;
using CrowdFunding.BuildingBlocks.Domain.Common;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCancelled;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignCreated;
using CrowdFunding.Modules.Campaigns.Contracts.Events.CampaignPublished;
using CrowdFunding.Modules.Campaigns.Domain.Events;
using CrowdFunding.Modules.Campaigns.Infrastructure.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore.Storage;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Transactions;

/// <summary>
/// Executes the surrounding module write operation inside a transaction boundary.
/// </summary>
public sealed class CampaignTransactionExecutor : ICampaignTransactionExecutor
{
    private readonly CampaignsDbContext _dbContext;

    public CampaignTransactionExecutor(CampaignsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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
            }
        }
    }

    private static OutboxMessage MapApplicationEvent(BaseEvent domainEvent)
    {
        return domainEvent switch
        {
            CampaignCreatedDomainEvent @event => OutboxMessage.Create(
                new CampaignCreatedApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            CampaignPublishedDomainEvent @event => OutboxMessage.Create(
                new CampaignPublishedApplicationEvent(@event.CampaignId, @event.OwnerId),
                DateTime.UtcNow),
            CampaignCancelledDomainEvent @event => OutboxMessage.Create(
                new CampaignCancelledApplicationEvent(@event.CampaignId, @event.OwnerId),
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
