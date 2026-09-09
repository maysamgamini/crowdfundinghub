namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;

/// <summary>
/// Defines the transaction boundary for campaign write operations.
/// </summary>
public interface ICampaignTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>
    /// Executes <paramref name="action"/> inside a transaction, first acquiring a PostgreSQL
    /// transaction-scoped advisory lock (<c>pg_advisory_xact_lock</c>) keyed by
    /// <paramref name="advisoryLockKey"/>. Serializes concurrent writers targeting the same
    /// logical resource (e.g. one campaign's balance) so the read-modify-write inside
    /// <paramref name="action"/> cannot race with another instance of itself. The lock is
    /// released automatically when the transaction commits or rolls back.
    /// </summary>
    Task<T> ExecuteAsync<T>(long advisoryLockKey, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>Non-generic overload for actions with no result to return, so callers don't have
    /// to end their lambda with an arbitrary dummy value just to satisfy <c>Task&lt;T&gt;</c>.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);

    /// <summary>Non-generic, advisory-lock-taking overload — see the locked
    /// <see cref="ExecuteAsync{T}(long, Func{CancellationToken, Task{T}}, CancellationToken)"/> overload's remarks.</summary>
    Task ExecuteAsync(long advisoryLockKey, Func<CancellationToken, Task> action, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a distributed cache key for eviction once the currently-executing
    /// <c>ExecuteAsync</c> call's transaction has actually committed (TICKET-037). Call this from
    /// inside the <c>action</c> delegate instead of evicting the cache directly — evicting before
    /// commit leaves a window where a concurrent read can repopulate the cache with the
    /// about-to-be-overwritten value, which would then serve stale data for a full TTL instead of
    /// until the next write.
    /// </summary>
    void EnqueueCacheInvalidation(string cacheKey);
}
