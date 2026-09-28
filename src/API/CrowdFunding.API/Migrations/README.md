# API Migrations and Seeding

## Purpose
Provides automated database schema migration runners and operational account seeding utilities executed at application startup or via dedicated CLI command arguments.

## Files
- `MigrationRunner.cs`: Discovers and executes pending Entity Framework Core migrations across all module `DbContext` instances (`CampaignsDbContext`, `ContributionsDbContext`, `IdentityDbContext`, `ModerationDbContext`, `NotificationsDbContext`, `CampaignUpdatesDbContext`) and the shared `AuditDbContext`. Invoked automatically in the `Development` and `Testing` environments and directly during deployment init-containers via `dotnet run -- migrate`. When `ConnectionStrings:Migrations` is set, migrations run with that owner role's user name and password (`crowdfunding_migrator`) on each module's own server and database, so the per-module runtime roles never need DDL rights (see `docs/data-ownership.md`).
- `AdminSeeder.cs`: Seeds the initial administrative user out-of-band via CLI execution (`dotnet run -- seed-admin <email> <password> <displayName>`). Guarantees that administrative privileges are provisioned securely by operators rather than via public API endpoints.

## Operational Modes
1. **CLI Database Migration**:
   ```bash
   dotnet run --project src/API/CrowdFunding.API -- migrate
   ```
   Applies pending migrations for all modular contexts sequentially and exits with code 0 upon success. Recommended for container pre-start hooks or Kubernetes job runners to avoid concurrent DDL execution across multiple web replicas.

2. **CLI Admin Provisioning**:
   ```bash
   dotnet run --project src/API/CrowdFunding.API -- seed-admin admin@crowdfunding.local "SecurePassword123!" "System Administrator"
   ```
   Normalizes the user email, creates or promotes the specified user to the `Admin` role via `SeedAdminCommand`, and persists the user record in `IdentityDbContext`.
