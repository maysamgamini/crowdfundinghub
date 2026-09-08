namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

/// <summary>
/// Defines the transaction boundary for contribution write operations.
/// </summary>
public interface IContributionTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>Non-generic overload for actions with no result to return, so callers don't have
    /// to end their lambda with an arbitrary dummy value just to satisfy <c>Task&lt;T&gt;</c>.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
