# CrowdFunding Hub

## Overview
CrowdFunding Hub is a modular ASP.NET Core backend for a crowdfunding platform. The solution is organized around feature modules instead of a single monolith: `Identity` handles authentication and authorization, `Campaigns` owns campaign lifecycle rules, `Moderation` tracks review decisions, `Contributions` manages payment-state transitions, and event-driven support modules react to those business events.

## Architecture
The runtime follows a clean, layered structure:
- `src/API/CrowdFunding.API` is the HTTP entry point.
- `src/BuildingBlocks` contains shared application, domain, and infrastructure primitives.
- `src/Modules/*` contains feature modules split into `Application`, `Contracts`, `Domain`, and `Infrastructure` projects.
- `tests/*` contains unit, architecture, and integration test projects.

Each module keeps domain rules in its domain project, use-case orchestration in its application project, external contracts in its contracts project, and EF Core plus service implementations in its infrastructure project. The API layer references application and contract layers, not domain projects directly.

## Runtime Flow
1. The API authenticates requests with JWT bearer tokens and converts claims into the shared `ICurrentUser` abstraction.
2. Controllers dispatch commands and queries through the shared dispatcher interfaces.
3. Command handlers load aggregates through repositories, enforce permissions and business rules, and execute inside module-specific transaction executors.
4. Domain events are captured on aggregates and written to outbox tables during the same database transaction.
5. `OutboxProcessorBackgroundService` polls unprocessed outbox rows and republishes them as application events for cross-module handlers.

## Modules
- `Identity`: registration, login, role assignment, permission grants, and JWT issuance.
- `Campaigns`: create, publish, cancel, query, and update raised totals.
- `Moderation`: create reviews from campaign creation events and approve or reject them.
- `Contributions`: create contributions, confirm or fail payment, and publish successful contribution events.
- `Notifications`: event handlers reserved for notification workflows.
- `CampaignUpdates`: event handlers reserved for campaign activity workflows.

## Technology Stack
- .NET 10 / ASP.NET Core
- Entity Framework Core 10 with PostgreSQL (`Npgsql`)
- JWT bearer authentication
- FluentValidation
- Mapster for contract mapping
- xUnit for tests

## Getting Started
### Prerequisites
- .NET SDK 10.x
- Docker Desktop or another way to run PostgreSQL

### Local database
Run the local PostgreSQL container from the repo root:

```powershell
docker compose up -d
```

The default connection string points to `localhost:5432` with database `crowdfundingdb` and credentials `postgres/postgres`.

### Run the API
From the repo root:

```powershell
dotnet run --project src/API/CrowdFunding.API/CrowdFunding.API.csproj
```

On startup in development, the API applies pending migrations for the campaign, contribution, identity, and moderation databases.

### Swagger and local settings
Swagger is enabled in development. JWT settings and the local connection string live in [appsettings.json](/c:/Repos/crowd-funding-hub/crowdfundinghub/src/API/CrowdFunding.API/appsettings.json:1).

## Testing
Use the solution file to run the full automated suite:

```powershell
dotnet test CrowdFunding.slnx
```

Test projects are separated by purpose:
- `tests/UnitTests`: domain and handler behavior.
- `tests/ArchitectureTests`: dependency-boundary guardrails.
- `tests/IntegrationTests`: end-to-end scaffolding.

## Documentation Map
- `DEV_GUIDELINES.md`: contributor conventions, workflows, and architectural rules.
- `README.md` files under `src` and `tests`: folder-by-folder explanations.
- XML documentation comments in source files: class-level intent for the main types.
