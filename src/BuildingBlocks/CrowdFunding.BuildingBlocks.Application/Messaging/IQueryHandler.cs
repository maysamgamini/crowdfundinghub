namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// Handles a specific query.
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : class
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
