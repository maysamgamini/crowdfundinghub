using System.Data.Common;
using System.Runtime.CompilerServices;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Calls <see cref="OutboxSignal{TDbContext}.Notify"/> once the rows it saw added to the outbox
/// are durable: after the surrounding transaction commits, or right after SaveChanges when there
/// is no explicit transaction.
/// </summary>
/// <remarks>
/// Signalling at SaveChanges time inside a transaction would wake the processor before the rows
/// are visible to its claim query, so it would find nothing and sleep until the fallback sweep.
/// Hooking the commit keeps the transaction executors unaware of the signal. A rolled-back
/// transaction discards the pending signal.
/// </remarks>
/// <typeparam name="TDbContext">The module DbContext this interceptor is attached to.</typeparam>
public sealed class OutboxCommitSignalInterceptor<TDbContext> : ISaveChangesInterceptor, IDbTransactionInterceptor
    where TDbContext : DbContext
{
    private static readonly object Pending = new();

    private readonly OutboxSignal<TDbContext> _signal;
    private readonly ConditionalWeakTable<DbContext, object> _awaitingCommit = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxCommitSignalInterceptor{TDbContext}"/> class.
    /// </summary>
    /// <param name="signal">The module's outbox signal.</param>
    public OutboxCommitSignalInterceptor(OutboxSignal<TDbContext> signal)
    {
        _signal = signal;
    }

    /// <inheritdoc/>
    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        TrackOutboxWrites(eventData.Context);
        return result;
    }

    /// <inheritdoc/>
    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        TrackOutboxWrites(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfAlreadyCommitted(eventData.Context);
        return result;
    }

    /// <inheritdoc/>
    public ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        NotifyIfAlreadyCommitted(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => NotifyIfPending(eventData.Context);

    /// <inheritdoc/>
    public Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        NotifyIfPending(eventData.Context);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => Discard(eventData.Context);

    /// <inheritdoc/>
    public Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void TrackOutboxWrites(DbContext? context)
    {
        if (context is not null && context.ChangeTracker.Entries<OutboxMessage>().Any(entry => entry.State == EntityState.Added))
        {
            _awaitingCommit.AddOrUpdate(context, Pending);
        }
    }

    private void NotifyIfAlreadyCommitted(DbContext? context)
    {
        // Inside an explicit transaction the rows are not visible yet; TransactionCommitted fires later.
        if (context?.Database.CurrentTransaction is null)
        {
            NotifyIfPending(context);
        }
    }

    private void NotifyIfPending(DbContext? context)
    {
        if (context is not null && _awaitingCommit.Remove(context))
        {
            _signal.Notify();
        }
    }

    private void Discard(DbContext? context)
    {
        if (context is not null)
        {
            _awaitingCommit.Remove(context);
        }
    }
}
