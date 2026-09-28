# Persistence

## Purpose
Groups persistence concerns for the Contributions module, including EF Core configuration, db contexts, read models, repositories, and migrations.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `Configurations`: Contains EF Core entity configuration classes for the Contributions module.
- `DbContexts`: Contains EF Core database contexts for the Contributions module.
- `Migrations`: Contains the EF Core migration history for the Contributions module. These files are generated snapshots of schema evolution.
- `ReadModels`: `ActiveCampaignCache.cs`, the module's local replica of the campaign fields needed to validate a pledge (table `active_campaigns_cache`), filled only from Campaigns' application events.
- `Repositories`: Contains repository implementations for the Contributions module.
