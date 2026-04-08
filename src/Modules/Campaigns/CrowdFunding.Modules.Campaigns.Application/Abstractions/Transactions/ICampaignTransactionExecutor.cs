namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;

public interface ICampaignTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
