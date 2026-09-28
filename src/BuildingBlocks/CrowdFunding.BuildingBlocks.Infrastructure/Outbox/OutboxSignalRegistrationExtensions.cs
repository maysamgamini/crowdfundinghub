using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Wires the commit-triggered outbox wake-up for one module.
/// </summary>
public static class OutboxSignalRegistrationExtensions
{
    /// <summary>
    /// Registers <see cref="OutboxSignal{TDbContext}"/> and its commit interceptor as singletons.
    /// </summary>
    /// <typeparam name="TDbContext">The module DbContext that owns an outbox table.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddOutboxSignal<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        services.AddSingleton<OutboxSignal<TDbContext>>();
        services.AddSingleton<OutboxCommitSignalInterceptor<TDbContext>>();
        return services;
    }

    /// <summary>
    /// Attaches the module's <see cref="OutboxCommitSignalInterceptor{TDbContext}"/> to its DbContext options.
    /// </summary>
    /// <typeparam name="TDbContext">The module DbContext that owns an outbox table.</typeparam>
    /// <param name="options">The DbContext options being configured.</param>
    /// <param name="serviceProvider">The provider passed to the <c>AddDbContext</c> callback.</param>
    /// <returns>The same options builder.</returns>
    public static DbContextOptionsBuilder AddOutboxSignalInterceptor<TDbContext>(
        this DbContextOptionsBuilder options, IServiceProvider serviceProvider)
        where TDbContext : DbContext
        => options.AddInterceptors(serviceProvider.GetRequiredService<OutboxCommitSignalInterceptor<TDbContext>>());
}
