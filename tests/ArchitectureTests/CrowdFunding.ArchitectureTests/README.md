# CrowdFunding Architecture Tests

## Purpose
Executable architecture tests using `NetArchTest.Rules` to enforce dependency boundaries, clean architecture invariants, and prevent layer coupling or improper cross-module references.

## Test Suites & Boundary Rules
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
- `ModerationModuleDependencyTests.cs`:
  - Enforces that `API` does not directly reference `Moderation.Domain`.
  - Verifies module boundary isolation between `Moderation` and peer business modules.

## Significance in CI/CD
These tests run on every pull request to detect accidental architectural drift or shortcut references before code can be merged.
