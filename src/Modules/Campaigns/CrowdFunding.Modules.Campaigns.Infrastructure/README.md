# Campaigns: Infrastructure Layer

## Purpose
Contains infrastructure implementations for the Campaigns module, including Entity Framework Core persistence, caching decorators, external service adapters, and transaction executors.

## Files
- `CrowdFunding.Modules.Campaigns.Infrastructure.csproj`: Project file defining dependencies for EF Core, Npgsql, caching, and common building blocks.

## Subdirectories
- `Caching`: Read-through distributed caching (`CachedCampaignReadService`) with active invalidation (`CampaignCacheKeys`).
- `DependencyInjection`: Extension methods (`AddCampaignsInfrastructure`) registering DbContexts, repositories, cached read services, and transaction executors.
- `Persistence`: EF Core `CampaignsDbContext`, configurations (`CampaignConfiguration`, `ContributionLedgerEntryConfiguration`), repositories (`CampaignRepository`), and SQL migrations.
- `Services`: System clock provider (`SystemCampaignsDateTimeProvider`).
- `Transactions`: `CampaignsTransactionExecutor` coordinating database transactions, domain event harvesting, and outbox persistence.
