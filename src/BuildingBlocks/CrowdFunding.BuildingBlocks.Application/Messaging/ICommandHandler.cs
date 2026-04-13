namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// Handles a specific command.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : class
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}
