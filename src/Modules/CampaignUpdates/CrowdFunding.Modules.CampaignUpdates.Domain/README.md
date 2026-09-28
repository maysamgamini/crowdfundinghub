# Crowd Funding Modules Campaign Updates Domain

## Purpose
Contains the core domain model for the Campaign Updates module: outbound webhook subscriptions and their delivery tasks. The module raises no domain events.

## Files
- `CrowdFunding.Modules.CampaignUpdates.Domain.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Aggregates`:
  - `WebhookSubscription.cs`: A creator-registered webhook target for one campaign, with its signing secret, active flag, and consecutive-failure count; it disables itself after too many consecutive failures.
  - `WebhookDeliveryTask.cs`: One pending delivery of a signed payload to a subscription, with status (`Pending`, `Processing`, `Delivered`, `Dead`), attempts, claim lease, and a fixed back-off schedule (1 min, 5 min, 15 min, 1 h) before it is marked dead.
