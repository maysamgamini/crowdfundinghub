using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Pagination;
using CrowdFunding.Modules.Campaigns.Application.Abstractions.Services;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.GetCampaignById;
using CrowdFunding.Modules.Campaigns.Application.Features.Campaigns.Queries.ListCampaigns;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Campaigns.Infrastructure.Caching;

/// <summary>
/// Read-through Redis cache in front of <see cref="ICampaignReadService"/>, scoped deliberately
/// to <see cref="GetByIdAsync"/> — the single-campaign detail page is the hot, high-fan-out read
/// during a viral campaign (improvement.md §5.2); <see cref="ListAsync"/> is filtered/paginated
/// in ways that are harder to invalidate correctly and lower-traffic per query, so it is left
/// uncached rather than building a speculative generic tagged-cache abstraction with no concrete
/// second consumer yet.
///
/// Cache invalidation is active, not just TTL-based: <see cref="Persistence.Repositories.CampaignRepository.UpdateAsync"/>
/// removes this campaign's entry on every write, so a confirmed pledge is reflected immediately
/// rather than waiting out the (short, 30s) TTL — the TTL exists only as a safety net against a
/// missed invalidation, not as the primary consistency mechanism.
/// </summary>
public sealed class CachedCampaignReadService : ICampaignReadService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ICampaignReadService _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedCampaignReadService> _logger;

    public CachedCampaignReadService(ICampaignReadService inner, IDistributedCache cache, ILogger<CachedCampaignReadService> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GetCampaignByIdResult?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var cacheKey = CampaignCacheKeys.Details(campaignId);

        var cached = await TryGetFromCacheAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await _inner.GetByIdAsync(campaignId, cancellationToken);

        if (result is not null)
        {
            await TrySetCacheAsync(cacheKey, result, cancellationToken);
        }

        return result;
    }

    public Task<PagedResult<ListCampaignsResult>> ListAsync(
        PageRequest pageRequest,
        ListCampaignsFilter filter,
        CancellationToken cancellationToken)
        => _inner.ListAsync(pageRequest, filter, cancellationToken);

    private async Task<GetCampaignByIdResult?> TryGetFromCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
            return cachedBytes is null
                ? null
                : JsonSerializer.Deserialize<GetCampaignByIdResult>(cachedBytes, SerializerOptions);
        }
        catch (Exception exception)
        {
            // Cache-aside with graceful degradation: a Redis outage must fall back to the
            // database, not take reads down with it.
            _logger.LogWarning(exception, "Failed to read campaign {CacheKey} from the distributed cache.", cacheKey);
            return null;
        }
    }

    private async Task TrySetCacheAsync(string cacheKey, GetCampaignByIdResult result, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result, SerializerOptions);
            await _cache.SetAsync(
                cacheKey,
                bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to write campaign {CacheKey} to the distributed cache.", cacheKey);
        }
    }
}
