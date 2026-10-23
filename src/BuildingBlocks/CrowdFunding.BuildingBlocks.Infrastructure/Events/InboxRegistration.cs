using System.Reflection;
using CrowdFunding.BuildingBlocks.Application.Events;
using CrowdFunding.BuildingBlocks.Application.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Retention;
using CrowdFunding.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Events;

/// <summary>Ties the event handlers of one module (identified by assembly) to that module's inbox.</summary>
public sealed record InboxBinding(Assembly HandlerAssembly, Type InboxType);

public static class InboxRegistration
{
    /// <summary>
    /// Makes every <see cref="IEventHandler{TEvent}"/> defined in <paramref name="handlerAssembly"/>
    /// idempotent per outbox message, recording its progress in <typeparamref name="TDbContext"/>'s
    /// inbox table. Independent of the order in which handlers are registered.
    /// </summary>
    public static IServiceCollection AddInbox<TDbContext>(this IServiceCollection services, Assembly handlerAssembly)
        where TDbContext : DbContext
    {
        services.AddScoped<EfInbox<TDbContext>>();
        services.AddSingleton(new InboxBinding(handlerAssembly, typeof(EfInbox<TDbContext>)));

        // The inbox only has to outlive the longest redelivery window, so old rows are pruned on a schedule.
        services.AddRetention();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRetentionTask, InboxPruneTask<TDbContext>>());
        return services;
    }
}
