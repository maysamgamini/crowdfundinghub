using System.Collections.Concurrent;
using System.Reflection;
using CrowdFunding.BuildingBlocks.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Publishes application events by resolving their handlers from the service provider.
/// </summary>
public sealed class ServiceProviderEventPublisher : IEventPublisher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethods = new();
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProviderEventPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve event handlers.</param>
    public ServiceProviderEventPublisher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
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
