# Configurations

## Purpose
Contains EF Core entity configuration classes for the Contributions module.

## Files
- `ActiveCampaignCacheConfiguration.cs`: Maps the replicated `ActiveCampaignCache` read model to `active_campaigns_cache`.
- `ContributionConfiguration.cs`: Maps `Contribution` to `contributions`, with indexes and the `xmin` concurrency token.

## Child Folders
- None.
