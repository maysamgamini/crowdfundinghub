namespace CrowdFunding.BuildingBlocks.Application.Messaging;

public interface IQueryDispatcher
{
    Task<TResult> QueryAsync<TResult>(object query, CancellationToken cancellationToken);
}
