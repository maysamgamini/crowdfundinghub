namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// Dispatches queries to their handlers.
/// </summary>
public interface IQueryDispatcher
{
    Task<TResult> QueryAsync<TResult>(object query, CancellationToken cancellationToken);
}
