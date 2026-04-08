namespace CrowdFunding.BuildingBlocks.Application.Messaging;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : class
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
