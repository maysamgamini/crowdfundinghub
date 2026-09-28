# Campaigns: Infrastructure Layer

## Purpose
Contains infrastructure implementations for the Campaigns module, including Entity Framework Core persistence, caching decorators, external service adapters, and transaction executors.

## Files
- `CrowdFunding.Modules.Campaigns.Infrastructure.csproj`: Project file defining dependencies for EF Core, Npgsql, caching, and common building blocks.

## Subdirectories
- `BackgroundServices`: `CampaignExpirationBackgroundService` (moves published campaigns past their deadline to Successful or Failed) and `RewardTierReservationScavengerBackgroundService` (releases abandoned reward-tier reservations).
- `Caching`: Read-through distributed caching (`CachedCampaignReadService`) with active invalidation (`CampaignCacheKeys`).
- `DependencyInjection`: Extension methods (`AddCampaignsInfrastructure`) registering the DbContext, repositories, the ledger, the cached read service, the transaction executor, and the module's hosted services.
- `Outbox`: `CampaignsOutboxBackgroundService`, the module's `ModuleOutboxProcessor` for `CampaignsDbContext`.
- `Persistence`: EF Core `CampaignsDbContext`, configurations (`CampaignConfiguration`, `CampaignLedgerEntryConfiguration`, reward-tier configurations), repositories (`CampaignRepository`, `RewardTierRepository`, `RewardTierReservationRepository`, `CampaignLedger`: appends ledger entries with `INSERT … ON CONFLICT DO NOTHING` and recomputes the raised-amount snapshot as `SUM(entries)`), and migrations (`AppendOnlyCampaignLedger` adds the ledger table, backfill, CHECK constraint and the append-only and snapshot triggers).
- `Services`: Campaign read model (`CampaignReadService`) and system clock provider (`SystemDateTimeProvider`).
- `Transactions`: `CampaignTransactionExecutor` coordinating database transactions, domain event harvesting, and outbox persistence.
