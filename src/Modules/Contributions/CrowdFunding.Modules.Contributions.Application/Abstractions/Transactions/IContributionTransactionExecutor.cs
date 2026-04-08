namespace CrowdFunding.Modules.Contributions.Application.Abstractions.Transactions;

public interface IContributionTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
