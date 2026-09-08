using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Registers application event handlers with dependency injection.
/// </summary>
public static class NotificationRegistrationExtensions
{
    public static IServiceCollection AddEventHandlersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var handlerInterfaceType = typeof(IEventHandler<>);

        var handlers = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => new
            {
                Implementation = type.AsType(),
                ServiceTypes = type.ImplementedInterfaces
                    .Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == handlerInterfaceType)
                    .ToArray()
            })
            .Where(x => x.ServiceTypes.Length > 0);

        foreach (var handler in handlers)
        {
            foreach (var serviceType in handler.ServiceTypes)
            {
                services.AddScoped(serviceType, handler.Implementation);
            }
        }

        return services;
    }

    /// <summary>
    /// Scans the given assemblies for every non-abstract <see cref="BaseApplicationEvent"/> and
    /// registers it in a singleton <see cref="EventTypeRegistry"/>, so outbox consumers can
    /// resolve a persisted event's stable discriminator back to its CLR type without reflecting
    /// on an assembly-qualified name.
    /// </summary>
    public static IServiceCollection AddEventTypeRegistry(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        var registry = new EventTypeRegistry();

        var eventTypes = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(BaseApplicationEvent).IsAssignableFrom(type));

        foreach (var eventType in eventTypes)
        {
            registry.Register(eventType.AsType());
        }

        services.AddSingleton(registry);

        return services;
    }
}
