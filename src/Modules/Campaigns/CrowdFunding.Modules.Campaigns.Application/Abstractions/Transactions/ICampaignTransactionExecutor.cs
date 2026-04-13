namespace CrowdFunding.Modules.Campaigns.Application.Abstractions.Transactions;

/// <summary>
/// Defines the transaction boundary for campaign write operations.
/// </summary>
public interface ICampaignTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
