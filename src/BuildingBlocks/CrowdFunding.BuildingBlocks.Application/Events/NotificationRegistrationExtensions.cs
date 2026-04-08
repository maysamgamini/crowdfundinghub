using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Events;

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
}