using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Subscribes to every registered <see cref="CacheInvalidationSubscription"/> on startup
/// (TICKET-037). Each replica of the monolith runs its own instance of this hosted service, all
/// subscribed to the same Redis channels — so a write on any one replica (e.g. a signing-key
/// rotation) fans out to every other replica's in-memory cache within the Pub/Sub round trip,
/// instead of each replica only ever seeing its own local state.
///
/// Resilience to Redis being unreachable at startup (TICKET-037 acceptance criterion 3): a failed
/// subscribe is logged, not thrown — this must never block application startup or crash the
/// process, since the whole point of caching is to be a performance optimization, not a
/// dependency the app can't run without. Each subscription's owner is expected to also run a
/// periodic time-based refresh (see <c>SigningKeyRefreshBackgroundService</c>) as a fallback for
/// exactly this case.
/// </summary>
public sealed class DistributedCacheInvalidationSubscriber : IHostedService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<CacheInvalidationSubscription> _subscriptions;
    private readonly ILogger<DistributedCacheInvalidationSubscriber> _logger;

    public DistributedCacheInvalidationSubscriber(
        IConnectionMultiplexer redis,
        IServiceProvider serviceProvider,
        IEnumerable<CacheInvalidationSubscription> subscriptions,
        ILogger<DistributedCacheInvalidationSubscriber> logger)
    {
        _redis = redis;
        _serviceProvider = serviceProvider;
        _subscriptions = subscriptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
        {
            try
            {
                var subscriber = _redis.GetSubscriber();
                await subscriber.SubscribeAsync(
                    RedisChannel.Literal(subscription.Channel),
                    (_, _) => HandleAsync(subscription));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to subscribe to cache invalidation channel {Channel}. This replica will rely on its periodic time-based refresh until Redis becomes reachable.",
                    subscription.Channel);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async void HandleAsync(CacheInvalidationSubscription subscription)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            await subscription.Handler(scope.ServiceProvider, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache invalidation handler for channel {Channel} failed.", subscription.Channel);
        }
    }
}
