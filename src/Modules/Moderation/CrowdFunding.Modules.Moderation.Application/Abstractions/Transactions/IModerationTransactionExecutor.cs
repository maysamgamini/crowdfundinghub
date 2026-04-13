namespace CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;

/// <summary>
/// Defines the transaction boundary for moderation write operations.
/// </summary>
public interface IModerationTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
