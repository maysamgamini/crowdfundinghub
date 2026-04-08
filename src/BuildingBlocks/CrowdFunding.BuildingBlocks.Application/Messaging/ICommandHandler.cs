namespace CrowdFunding.BuildingBlocks.Application.Messaging;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : class
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}
