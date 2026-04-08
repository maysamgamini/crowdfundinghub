namespace CrowdFunding.Modules.Moderation.Application.Abstractions.Transactions;

public interface IModerationTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
