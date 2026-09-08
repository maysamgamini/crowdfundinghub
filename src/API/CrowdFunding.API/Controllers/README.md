# API Controllers

## Purpose
Hosts ASP.NET Core REST API controllers providing HTTP route endpoints for the system. Following `DEV_GUIDELINES.md`, controllers stay strictly thin: they handle parameter binding, perform request validation, dispatch commands or queries through `ICommandDispatcher` / `IQueryDispatcher`, and map domain/application results to HTTP response contracts.

## Controllers
- `CampaignsController.cs`: Exposes campaign management routes:
  - `POST /api/campaigns`: Create a new campaign draft.
  - `GET /api/campaigns/{id}`: Retrieve campaign details by ID.
  - `GET /api/campaigns`: Paginated list of campaigns with status/category filtering.
  - `POST /api/campaigns/{id}/publish`: Publish a draft campaign (enforces owner authorization).
  - `POST /api/campaigns/{id}/cancel`: Cancel an active or draft campaign (enforces owner/admin authorization).
  - `GET /api/campaigns/{id}/contribution-availability`: Check remaining funding headroom and pledge eligibility.
- `ContributionsController.cs`: Exposes contribution and pledge routes:
  - `POST /api/contributions`: Create a new contribution pledge (protected by `payment-strict` rate limiting).
  - `POST /api/contributions/{id}/confirm-payment`: Confirm successful third-party payment processing.
  - `POST /api/contributions/{id}/fail-payment`: Mark a contribution payment as failed.
  - `POST /api/contributions/{id}/cancel`: Cancel a pending contribution before settlement.
- `IdentityController.cs`: Exposes authentication and user identity routes:
  - `POST /api/identity/register`: Register a new user account (protected by `auth-strict` rate limiting).
  - `POST /api/identity/login`: Authenticate credentials and issue an asymmetric ES256 JWT access token (protected by `auth-strict` rate limiting).
  - `GET /api/identity/me`: Retrieve profile details for the currently authenticated user.
- `ModerationController.cs`: Exposes administrative and moderator review workflows:
  - `GET /api/moderation/reviews/campaigns/{campaignId}`: Retrieve full review details for a campaign (requires `Reviews.Read` permission).
  - `GET /api/moderation/reviews/campaigns/{campaignId}/status`: Check moderation review status for a campaign.
  - `POST /api/moderation/reviews/{campaignId}/approve`: Approve a pending campaign review (requires `Reviews.Approve` permission).
  - `POST /api/moderation/reviews/{campaignId}/reject`: Reject a campaign submission with review notes (requires `Reviews.Reject` permission).

## Design Conventions
- **Thin Controller Pattern**: Business logic, aggregate mutation, and transactional boundaries are encapsulated in application command handlers.
- **Route Attributes & Status Codes**: Action methods use explicit HTTP verbs (`HttpGet`, `HttpPost`), status codes (`200 OK`, `201 Created`, `400 BadRequest`, `404 NotFound`, `409 Conflict`), and typed `ProducesResponseType` annotations for Swagger documentation.
