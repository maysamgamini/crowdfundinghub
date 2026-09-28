# Database Provisioning

## Purpose
SQL that runs outside EF Core migrations because it needs a superuser: the per-module database roles that make PostgreSQL enforce data ownership. See [`docs/data-ownership.md`](../docs/data-ownership.md), section 5.

## Files
- `init/01-module-roles.sql`: Creates `crowdfunding_migrator` (owns every module schema and object, runs migrations) and one runtime role per module (`campaigns_app`, `contributions_app`, `identity_app`, `moderation_app`, `notifications_app`, `campaignupdates_app`) plus `audit_app`. Grants each runtime role `USAGE` and DML on its own schema only (`SELECT, INSERT` on `system.audit_records` for `audit_app`) and sets default privileges for tables that later migrations create. Idempotent; on a database migrated before roles existed it also hands existing module objects to the migrator. Sets no passwords.
- `init/02-dev-passwords.sql`: Development and test passwords (`<role>_dev`) matching `appsettings.Development.json`. Never run it against a shared or production database.

## How it runs
- `docker compose up -d` mounts `init/` into the Postgres entrypoint, which runs the scripts in name order when the data volume is created.
- The integration tests run the same files in every Testcontainers database (`ModuleDatabaseRoles.ProvisionAsync`).
- Deployed environments run `01-module-roles.sql` once per database as a superuser and set passwords from their secret store.
