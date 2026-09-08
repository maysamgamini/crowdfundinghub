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
}
