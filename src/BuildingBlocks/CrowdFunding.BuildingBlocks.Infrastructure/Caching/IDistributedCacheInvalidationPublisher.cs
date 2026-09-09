namespace CrowdFunding.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Publishes a fire-and-forget notification on a Redis Pub/Sub channel so every other running
/// instance of the monolith can react (TICKET-037). Pub/Sub, not the cache store itself, is the
/// right primitive here: the payload isn't cached data to read back, it's an instruction — "your
/// copy of X is stale, go reload it" — and every subscribed replica needs to receive it, not just
/// whichever one happens to read a key next.
/// </summary>
public interface IDistributedCacheInvalidationPublisher
{
    Task PublishAsync(string channel, CancellationToken cancellationToken);
}
