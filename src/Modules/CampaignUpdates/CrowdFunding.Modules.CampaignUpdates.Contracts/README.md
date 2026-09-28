# CampaignUpdates Contracts

## Purpose
The public contract of the CampaignUpdates module. CampaignUpdates publishes no application events; its consumers are campaign owners' own HTTP endpoints, so this project describes the outbound webhooks they receive.

## Files
- `Webhooks/WebhookEventTypes.cs`: The `eventType` values a subscriber can receive (`pledge.confirmed`).
- `Webhooks/PledgeConfirmedWebhookPayload.cs`: The camelCase JSON body of a `pledge.confirmed` delivery.
- `Webhooks/WebhookSignature.cs`: The `X-CrowdFunding-Signature` header and how to verify it (`t={unixSeconds},v1={hex HMAC-SHA256 of "{t}.{body}"}`).

## Dependency rule
No project references beyond the BCL, like every Contracts project. Other modules may reference this project; never the module's Application, Domain or Infrastructure (enforced by `ModuleBoundaryConventionTests`).
