# API Controllers (kept for comparison)

## Purpose
The API is served by Minimal APIs (see [`../Endpoints/`](../Endpoints/README.md)). This folder keeps exactly **one** MVC controller on purpose, so the two hosting styles can be compared on the same codebase, the same pipeline and the same kind of endpoint.

## Files
- `ModerationController.cs`: The moderation review queue (`/api/moderation/reviews`): a paged list, a get-by-campaign, and approve/reject actions behind the `moderation:review` permission policy. Its shape matches `Endpoints/CampaignEndpoints.cs`, the Minimal API to read it against.

## What stays identical in both styles
- Validation (`ValidationPipelineBehavior`), auditing, the handlers and the domain: the controller and the endpoints dispatch the same commands and queries.
- Errors: `GlobalExceptionHandler` turns exceptions into RFC 9457 ProblemDetails for both.
- OpenAPI: both are described in `/openapi/v1.json` with summaries from XML comments and the bearer scheme where authentication is required.
- Rate limiting, CORS, authentication and authorization middleware.

`ApiHostDependencyTests.Api_ShouldExposeMinimalApis_WithExactlyOneControllerKeptForComparison` fails if another controller is added without updating the test and [`docs/minimal-apis-vs-controllers.md`](../../../../docs/minimal-apis-vs-controllers.md).
