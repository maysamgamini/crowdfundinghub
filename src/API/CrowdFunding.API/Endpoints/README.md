# Minimal API Endpoints

## Purpose
Every HTTP endpoint of the API except the one comparison controller (`../Controllers/ModerationController.cs`). Endpoints are thin adapters: bind the request, build a command or query, dispatch it, map the result to a response contract. Validation runs in the command pipeline and errors become ProblemDetails in `GlobalExceptionHandler`, so handlers describe only the success path.

## Conventions
- One static class per module area with a `Map…Endpoints` extension that creates a route group (`MapGroup`) with its tag, and one `internal static` handler method per operation (the .NET 10 OpenAPI source generator reads their XML comments; it skips private members).
- Handlers return `TypedResults` (`Ok<T>`, `CreatedAtRoute<T>`, `NoContent`, `Results<…>` unions), which also produce the OpenAPI response metadata; `.ProducesProblem(...)`/`.ProducesValidationProblem()` declare the error responses the exception handler can return.
- Authorization is declared on the endpoint (`RequireAuthorization(PermissionConstants.X)`, `AllowAnonymous()`); ownership checks live in the handlers through `ICurrentUser`, which reads only the JWT `sub` claim.
- `Location` headers come from `CreatedAtRoute` with a named GET route, never a hand-built string.
- Explicit mapping from application results to response records (no mapping library).

## Files
- `EndpointRegistration.cs`: `MapCrowdFundingEndpoints` maps every group.
- `IdentityEndpoints.cs`: `/api/identity` — register, login, refresh, logout, me, role/permission/deactivate administration.
- `CampaignEndpoints.cs`: `/api/campaigns` — list, get, create, publish, cancel.
- `RewardTierEndpoints.cs`: `/api/campaigns/{campaignId}/reward-tiers` — list, get, create, reserve.
- `ContributionEndpoints.cs`: `/api/campaigns/{campaignId}/contributions` — list, get, pledge, confirm/fail payment.
- `WebhookSubscriptionEndpoints.cs`: `/api/campaigns/{campaignId}/webhook-subscriptions` — register, list, get, delete.
- `NotificationEndpoints.cs`: `/api/notifications` — get/update preferences, one-click unsubscribe.
- `InboundWebhookEndpoints.cs`: `/api/webhooks` — HMAC-verified payment gateway and media-analysis callbacks (anonymous, `webhook-strict` rate limit).
