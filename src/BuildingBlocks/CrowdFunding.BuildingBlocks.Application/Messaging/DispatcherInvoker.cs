using System.Reflection;

namespace CrowdFunding.BuildingBlocks.Application.Messaging;

internal static class DispatcherInvoker
{
    private static readonly MethodInfo HandleMethod = typeof(DispatcherInvoker)
        .GetMethod(nameof(InvokeCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Task<TResult> InvokeAsync<TMarker, TResult>(
        IServiceProvider serviceProvider,
        Type openHandlerType,
        object request,
        CancellationToken cancellationToken)
        where TMarker : class
    {
        var closedMethod = HandleMethod.MakeGenericMethod(request.GetType(), typeof(TResult));

        return (Task<TResult>)closedMethod.Invoke(
            null,
            [serviceProvider, openHandlerType, request, cancellationToken])!;
    }

    private static Task<TResult> InvokeCoreAsync<TRequest, TResult>(
        IServiceProvider serviceProvider,
        Type openHandlerType,
        object request,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        var handlerType = openHandlerType.MakeGenericType(typeof(TRequest), typeof(TResult));
        var handler = serviceProvider.GetService(handlerType)
                      ?? throw new InvalidOperationException(
                          $"No handler was registered for request '{typeof(TRequest).FullName}'.");

        var handleMethod = handlerType.GetMethod(nameof(ICommandHandler<TRequest, TResult>.Handle))
                          ?? throw new InvalidOperationException(
                              $"Handler '{handlerType.FullName}' does not expose a Handle method.");

        return (Task<TResult>)handleMethod.Invoke(handler, [request, cancellationToken])!;
    }
}
