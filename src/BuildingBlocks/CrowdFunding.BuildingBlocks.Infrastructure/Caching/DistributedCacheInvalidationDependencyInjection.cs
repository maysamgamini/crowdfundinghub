using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Wires up the Redis Pub/Sub distributed cache invalidation mechanism (TICKET-037). Call
/// <see cref="AddDistributedCacheInvalidation"/> once from the composition root, then have each
/// module register its own channel(s) via <see cref="AddCacheInvalidationSubscription"/>.
/// </summary>
public static class DistributedCacheInvalidationDependencyInjection
{
    public static IServiceCollection AddDistributedCacheInvalidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis")
                                     ?? throw new InvalidOperationException("Connection string 'Redis' was not found.");

        // A single shared multiplexer for Pub/Sub, registered once regardless of how many modules
        // call this method or AddStackExchangeRedisCache — StackExchange.Redis's own guidance is
        // one long-lived ConnectionMultiplexer per process, reused across all callers, rather than
        // one per consumer.
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.TryAddSingleton<IDistributedCacheInvalidationPublisher, RedisDistributedCacheInvalidationPublisher>();
        services.AddHostedService<DistributedCacheInvalidationSubscriber>();

        return services;
    }

    /// <summary>
    /// Registers <paramref name="handler"/> to run (in a fresh DI scope) whenever a message
    /// arrives on <paramref name="channel"/>. Safe to call multiple times for different channels;
    /// each becomes its own subscription.
    /// </summary>
    public static IServiceCollection AddCacheInvalidationSubscription(
        this IServiceCollection services,
        string channel,
        Func<IServiceProvider, CancellationToken, Task> handler)
    {
        services.AddSingleton(new CacheInvalidationSubscription(channel, handler));
        return services;
    }
}
