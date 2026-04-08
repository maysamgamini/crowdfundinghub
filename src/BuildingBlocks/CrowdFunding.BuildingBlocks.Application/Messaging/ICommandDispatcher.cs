namespace CrowdFunding.BuildingBlocks.Application.Messaging;

public interface ICommandDispatcher
{
    Task<TResult> SendAsync<TResult>(object command, CancellationToken cancellationToken);
}
