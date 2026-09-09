using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Publishes invalidation notifications over the shared Redis connection. A failed publish (e.g.
/// Redis is momentarily unreachable) is logged and swallowed rather than surfaced to the caller —
/// the write that triggered it has already committed to PostgreSQL by the time this runs (see
/// <c>CampaignTransactionExecutor</c>/<c>EfSigningKeyStore.RotateAsync</c>), so failing the whole
/// operation over a best-effort cache-coherence signal would be strictly worse than the
/// temporary staleness it would otherwise cause; the periodic TTL-based refresh
/// (<c>SigningKeyRefreshBackgroundService</c>) exists specifically to bound that staleness when
/// Pub/Sub delivery is missed.
/// </summary>
public sealed class RedisDistributedCacheInvalidationPublisher : IDistributedCacheInvalidationPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedCacheInvalidationPublisher> _logger;

    public RedisDistributedCacheInvalidationPublisher(
        IConnectionMultiplexer redis,
        ILogger<RedisDistributedCacheInvalidationPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task PublishAsync(string channel, CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetSubscriber().PublishAsync(RedisChannel.Literal(channel), string.Empty);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to publish cache invalidation on channel {Channel}.", channel);
        }
    }
}
