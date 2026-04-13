using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// Dispatches command objects to their registered handlers.
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> SendAsync<TResult>(object command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return DispatcherInvoker.InvokeAsync<ICommandHandler<TResultMarker, TResult>, TResult>(
            _serviceProvider,
            typeof(ICommandHandler<,>),
            command,
            cancellationToken);
    }

    private sealed class TResultMarker
    {
    }
}
