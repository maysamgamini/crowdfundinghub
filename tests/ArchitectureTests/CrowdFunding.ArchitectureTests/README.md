# CrowdFunding Architecture Tests

## Purpose
Executable architecture tests that inspect compiled assembly references (`Assembly.GetReferencedAssemblies()`, no third-party rules library) to enforce dependency boundaries, clean architecture invariants, and prevent layer coupling or improper cross-module references.

## Test Suites & Boundary Rules
- `ModuleBoundaryConventionTests.cs` (convention-based, covers every module automatically, including ones added later):
  - Every module has exactly the four layers: Contracts, Domain, Application, Infrastructure.
  - A module references another module only through that module's `Contracts`.
  - A Domain assembly references no CrowdFunding assembly other than `BuildingBlocks.Domain`.
  - A Contracts assembly references no module's internals.
  These rules are what keep any module extractable into its own service later.
- `ModuleSchemaOwnershipTests.cs` (convention-based, builds each module DbContext's EF model without a database):
  - Every module has exactly one DbContext.
  - Every module DbContext has a default schema named after the module.
  - No two modules share a schema.
  - Every table of a module maps into that module's schema, and no module maps a table into another module's schema.
  See `docs/data-ownership.md`.
- `CampaignsModuleDependencyTests.cs`:
  - Enforces that `API` does not directly reference `Campaigns.Domain`.
  - Asserts that `Campaigns.Domain` has no dependencies on `Campaigns.Infrastructure` or `API`.
  - Asserts that external modules only consume `Campaigns.Contracts`.
- `ContributionsModuleDependencyTests.cs`:
  - Enforces that `API` does not directly reference `Contributions.Domain`.
  - Asserts that `Contributions.Domain` has no dependencies on `Contributions.Infrastructure` or other modules.
  - Asserts that external modules interact exclusively through `Contributions.Contracts`.
- `IdentityModuleDependencyTests.cs`:
  - Enforces that `API` does not directly reference `Identity.Domain`.
  - Guarantees `Identity.Domain` contains no external framework dependencies.
  - Ensures other business modules interact solely via `Identity.Contracts`.
- `CampaignUpdatesModuleDependencyTests.cs` / `NotificationsModuleDependencyTests.cs`:
  - Domain does not reference Application or Infrastructure; Application does not reference other modules' internals.
- `ReplicatedReadModelBoundaryTests.cs`:
  - Contributions validates pledges against its own replicated campaign snapshot, never a synchronous query into Campaigns.
- `ApiHostDependencyTests.cs`:
  - The API host references no teaching sample (the Rosetta Stone sample is its own host).
  - The API exposes Minimal APIs with exactly one controller, `ModerationController`, kept for comparison.
- `ModerationModuleDependencyTests.cs`:
  - Enforces that `API` does not directly reference `Moderation.Domain`.
  - Verifies module boundary isolation between `Moderation` and peer business modules.

## Significance in CI/CD
These tests run on every pull request to detect accidental architectural drift or shortcut references before code can be merged.
