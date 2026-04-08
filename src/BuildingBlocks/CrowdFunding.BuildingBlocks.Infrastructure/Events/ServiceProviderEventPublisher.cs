using System.Collections.Concurrent;
using System.Reflection;
using CrowdFunding.BuildingBlocks.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Events;

public sealed class ServiceProviderEventPublisher : IEventPublisher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethods = new();
    private readonly IServiceProvider _serviceProvider;

    public ServiceProviderEventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var eventType = notification.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType);
        var handleMethod = HandleMethods.GetOrAdd(
            handlerType,
            static type => type.GetMethod(nameof(IEventHandler<object>.Handle))
                           ?? throw new InvalidOperationException($"Handler type '{type.FullName}' does not expose Handle."));

        foreach (var handler in handlers)
        {
            var task = (Task)handleMethod.Invoke(handler, [notification, cancellationToken])!;
            await task;
        }
    }
}