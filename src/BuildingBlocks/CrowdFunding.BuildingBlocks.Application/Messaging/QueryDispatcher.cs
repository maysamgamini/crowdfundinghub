using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Messaging;

public sealed class QueryDispatcher : IQueryDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public QueryDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResult> QueryAsync<TResult>(object query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return DispatcherInvoker.InvokeAsync<IQueryHandler<TResultMarker, TResult>, TResult>(
            _serviceProvider,
            typeof(IQueryHandler<,>),
            query,
            cancellationToken);
    }

    private sealed class TResultMarker
    {
    }
}
