# Campaigns Distributed Caching

## Purpose
Provides high-performance distributed caching (via `IDistributedCache` / Redis / in-memory fallback) for high-traffic campaign query endpoints, featuring proactive write-side cache invalidation.

## Files
- `CachedCampaignReadService.cs`: Read-through decorator for `ICampaignReadService.GetByIdAsync`. Caches campaign detail projections with a 30-second TTL safety net and provides graceful degradation: cache read/write failures are logged as warnings and fall back directly to PostgreSQL rather than aborting requests.
- `CampaignCacheKeys.cs`: Centralizes distributed cache key formatting (`campaigns:{campaignId}:details`) shared between the read decorator and `CampaignRepository.UpdateAsync` invalidation.

## Invalidation Architecture
Cache entries are actively purged whenever a campaign is modified (e.g. status changes, goal adjustments, or confirmed pledges applied). This ensures users see updated totals immediately while still shielding the primary database from read spikes during viral campaigns.
