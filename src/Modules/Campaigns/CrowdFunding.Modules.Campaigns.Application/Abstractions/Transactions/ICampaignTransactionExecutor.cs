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
}
