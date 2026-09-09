using CrowdFunding.Modules.Identity.Application.Abstractions.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace CrowdFunding.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Fast-path (TICKET-036) distributed revocation blacklist backed by Redis
/// (<see cref="IDistributedCache"/>, registered against the shared Redis instance in
/// <c>IdentityInfrastructureDependencyInjection</c>). A revoked stamp is kept only for the
/// access-token TTL: any token embedding it would fail signature/lifetime validation on its own
/// after that window regardless, so there is no need to remember it for longer.
/// </summary>
public sealed class RedisSecurityStampRevocationStore : ISecurityStampRevocationStore
{
    private readonly IDistributedCache _cache;
    private readonly TimeSpan _ttl;

    public RedisSecurityStampRevocationStore(IDistributedCache cache, IConfiguration configuration)
    {
        _cache = cache;
        var expirationMinutes = int.TryParse(configuration[$"{JwtOptions.SectionName}:ExpirationMinutes"], out var minutes)
            ? minutes
            : 15;
        _ttl = TimeSpan.FromMinutes(expirationMinutes);
    }

    public Task RevokeAsync(Guid userId, Guid revokedStamp, CancellationToken cancellationToken)
        => _cache.SetStringAsync(
            Key(userId, revokedStamp),
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _ttl },
            cancellationToken);

    public async Task<bool> IsRevokedAsync(Guid userId, Guid stamp, CancellationToken cancellationToken)
        => await _cache.GetStringAsync(Key(userId, stamp), cancellationToken) is not null;

    private static string Key(Guid userId, Guid stamp) => $"revoked_stamps:{userId}:{stamp}";
}
