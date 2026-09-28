# Persistence

## Purpose
Defines persistence ports consumed by the Contributions application layer so handlers stay independent from EF Core details.

## Files
- `ActiveCampaignSnapshot.cs`: Read-only snapshot of a campaign as replicated into the module's own `active_campaigns_cache`.
- `IActiveCampaignCacheRepository.cs`: Reads and writes the local campaign replica used to validate pledges without a synchronous call into Campaigns.
- `IContributionRepository.cs`: Persistence operations for the `Contribution` aggregate.

## Child Folders
- None.
