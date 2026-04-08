using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Application.Messaging;

public static class RequestHandlerRegistrationExtensions
{
    public static IServiceCollection AddRequestHandlersFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        RegisterHandlers(services, assemblies, typeof(ICommandHandler<,>));
        RegisterHandlers(services, assemblies, typeof(IQueryHandler<,>));

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, IEnumerable<Assembly> assemblies, Type openHandlerType)
    {
        var handlers = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => new
            {
                Implementation = type.AsType(),
                ServiceTypes = type.ImplementedInterfaces
                    .Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition() == openHandlerType)
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
    }
}
