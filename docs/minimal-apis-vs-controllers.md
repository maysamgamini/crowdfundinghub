# Minimal APIs vs. Controllers

The API host serves every endpoint as a Minimal API ([`src/API/CrowdFunding.API/Endpoints/`](../src/API/CrowdFunding.API/Endpoints/README.md)) except one MVC controller kept on purpose for comparison: [`ModerationController`](../src/API/CrowdFunding.API/Controllers/ModerationController.cs). Read it next to [`CampaignEndpoints`](../src/API/CrowdFunding.API/Endpoints/CampaignEndpoints.cs): both have a paged list, a get-by-id and POST actions behind permission policies.

## Side by side

| Concern | Controller (`ModerationController`) | Minimal API (`CampaignEndpoints`) |
| :--- | :--- | :--- |
| Routing | `[Route]`, `[HttpPost("{campaignId:guid}/approve")]` attributes | `MapGroup("/api/campaigns")`, `group.MapPost("/{campaignId:guid}/publish", PublishAsync)` |
| Dependencies | Constructor injection into fields, shared by every action | Handler parameters, per operation (only what that handler uses) |
| Authorization | `[Authorize(Policy = PermissionConstants.ModerationReview)]` | `.RequireAuthorization(PermissionConstants.CampaignsPublish)` |
| Rate limiting | `[EnableRateLimiting(...)]` | `.RequireRateLimiting(...)` |
| Result type | `ActionResult<T>`, `Ok(...)` | `Task<Ok<T>>`, `TypedResults.Ok(...)` (the signature is the contract) |
| Created + Location | `CreatedAtAction(nameof(GetById), ...)` | `TypedResults.CreatedAtRoute(response, "GetCampaignById", ...)` |
| OpenAPI responses | `[ProducesResponseType(...)]` attributes | Inferred from `TypedResults`, plus `.ProducesProblem(...)` |
| Summaries | XML comments on actions | XML comments on `internal static` handler methods |
| Model validation | None needed: `ValidationPipelineBehavior` validates commands | Same |
| Errors | `GlobalExceptionHandler` → ProblemDetails | Same |
| Infrastructure cost | `AddControllers()` + MVC pipeline (filters, model binding, `ApiController` conventions) | Routing only; source-generated request delegates, AOT-friendly |

## Why Minimal APIs here
- The endpoints are thin adapters by design (the business logic is in handlers and aggregates), so the controller class, its constructor and MVC's filter pipeline add ceremony without adding capability.
- `TypedResults` put the response contract in the method signature, where the compiler checks it and OpenAPI reads it.
- Per-handler dependencies make each operation's needs explicit; a controller's constructor grows with the union of all its actions.
- Route groups apply shared metadata (tag, authorization, rate limiting, error responses) once.

## When a controller is still the better fit
- A team with a large existing MVC codebase and conventions built on filters, `ApiController` behaviors or model binders.
- Endpoints that rely on MVC-only features: action filters, custom model binders, content negotiation beyond JSON, views.
- Very large resource surfaces where a class per resource is the organizing unit people expect (route groups plus one static class per area get close to this).

Nothing else changes between the two styles in this codebase: the same command/query dispatch, validation, auditing, ProblemDetails, OpenAPI document, authentication, authorization and rate limiting apply to both. `ApiHostDependencyTests` pins the controller count at one so the comparison stays deliberate.
