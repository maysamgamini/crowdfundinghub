using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Messaging;

/// <summary>
/// Invokes handlers dynamically for the shared command and query dispatchers.
/// </summary>
internal static class DispatcherInvoker
{
    private static readonly MethodInfo HandleMethod = typeof(DispatcherInvoker)
        .GetMethod(nameof(InvokeCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static Task<TResult> InvokeAsync<TMarker, TResult>(
        IServiceProvider serviceProvider,
        Type openHandlerType,
        object request,
        CancellationToken cancellationToken,
        Type? openBehaviorType = null)
        where TMarker : class
    {
        var closedMethod = HandleMethod.MakeGenericMethod(request.GetType(), typeof(TResult));

        return (Task<TResult>)closedMethod.Invoke(
            null,
            [serviceProvider, openHandlerType, request, cancellationToken, openBehaviorType])!;
    }

    private static Task<TResult> InvokeCoreAsync<TRequest, TResult>(
        IServiceProvider serviceProvider,
        Type openHandlerType,
        object request,
        CancellationToken cancellationToken,
        Type? openBehaviorType)
        where TRequest : class
    {
        var handlerType = openHandlerType.MakeGenericType(typeof(TRequest), typeof(TResult));
        var handler = serviceProvider.GetService(handlerType)
                      ?? throw new InvalidOperationException(
                          $"No handler was registered for request '{typeof(TRequest).FullName}'.");

        var handleMethod = handlerType.GetMethod(nameof(ICommandHandler<TRequest, TResult>.Handle))
                          ?? throw new InvalidOperationException(
                              $"Handler '{handlerType.FullName}' does not expose a Handle method.");

        CommandHandlerDelegate<TResult> callHandler =
            () => (Task<TResult>)handleMethod.Invoke(handler, [request, cancellationToken])!;

        if (openBehaviorType is null)
        {
            return callHandler();
        }

        var behaviorType = openBehaviorType.MakeGenericType(typeof(TRequest), typeof(TResult));
        var behaviors = serviceProvider.GetServices(behaviorType).ToArray();

        if (behaviors.Length == 0)
        {
            return callHandler();
        }

        var behaviorHandleMethod = behaviorType.GetMethod(
            nameof(ICommandPipelineBehavior<TRequest, TResult>.HandleAsync))!;

        // Chain behaviors outermost-first in registration order: the last one built wraps every
        // behavior before it, so it must be built from the *first* registered behavior, meaning
        // we iterate the resolved list back-to-front while composing.
        var pipeline = callHandler;
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = () => (Task<TResult>)behaviorHandleMethod.Invoke(
                behavior, [request, next, cancellationToken])!;
        }

        return pipeline();
    }
}
