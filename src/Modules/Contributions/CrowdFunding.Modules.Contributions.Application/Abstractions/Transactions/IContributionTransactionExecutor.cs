namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

/// <summary>
/// Defines the transaction boundary for contribution write operations.
/// </summary>
public interface IContributionTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
