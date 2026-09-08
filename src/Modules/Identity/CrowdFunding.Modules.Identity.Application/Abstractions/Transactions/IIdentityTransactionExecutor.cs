namespace CrowdFunding.Modules.Identity.Application.Abstractions.Transactions;

/// <summary>
/// Defines the transaction boundary for identity write operations. Brings Identity in line with
/// Campaigns/Contributions/Moderation, where <c>SaveChangesAsync</c> is always the transaction
/// executor's responsibility, never the repository's — previously <c>UserRepository.AddAsync</c>
/// and <c>UpdateAsync</c> called <c>SaveChangesAsync</c> directly, so a handler that needed to
/// coordinate a User change with anything else in the same unit of work would have had that
/// write commit prematurely, ahead of and independent from the rest of the transaction.
/// </summary>
public interface IIdentityTransactionExecutor
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>Non-generic overload for actions with no result to return, so callers don't have
    /// to end their lambda with an arbitrary dummy value just to satisfy <c>Task&lt;T&gt;</c>.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}
