namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// Dispatches commands to their handlers.
/// </summary>
public interface ICommandDispatcher
{
    Task<TResult> SendAsync<TResult>(object command, CancellationToken cancellationToken);
}
