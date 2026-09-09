namespace CrowdFunding.BuildingBlocks.Infrastructure.Caching;

/// <summary>
/// Declares that <paramref name="Handler"/> should run, inside a fresh DI scope, whenever a
/// message arrives on <paramref name="Channel"/>. Modules register these (see
/// <c>AddCacheInvalidationSubscription</c>) rather than opening their own Redis subscriptions, so
/// <see cref="DistributedCacheInvalidationSubscriber"/> can own connection lifetime and failure
/// handling for all of them in one place.
/// </summary>
public sealed record CacheInvalidationSubscription(string Channel, Func<IServiceProvider, CancellationToken, Task> Handler);
