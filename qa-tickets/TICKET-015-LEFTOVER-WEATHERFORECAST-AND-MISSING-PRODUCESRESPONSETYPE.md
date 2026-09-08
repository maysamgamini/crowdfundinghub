# QA Ticket: TICKET-015

**Title:** Leftover Scaffold Controller and Incomplete OpenAPI Error/Security Response Contract Annotations  
**Severity:** 🟡 P2 (Medium - API Cleanliness & Contract Completeness)  
**QA Focus Area:** API Contracts & Hygiene  
**Found By:** `qa-api-contracts`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description

### Issue A: Residual Scaffold `WeatherForecast` Files
In `src/API/CrowdFunding.API/`:
- `Controllers/WeatherForecastController.cs`
- `WeatherForecast.cs`

These files are standard `dotnet new webapi` template artifacts that were never removed. As a consequence:
- An unauthenticated `/WeatherForecast` endpoint is exposed on the production API surface.
- The Swagger / OpenAPI definition includes weather forecast models and endpoints completely unrelated to the CrowdFunding domain.

### Issue B: Missing OpenAPI Security and Error Response Type Annotations
While endpoints have `[ProducesResponseType]` for `200 OK`, `201 Created`, and `400 BadRequest` (ValidationProblemDetails), they omit annotations for standard security and domain failure responses:
- `[Authorize]`-protected endpoints (such as `POST /api/campaigns`, `POST /api/campaigns/{id}/publish`, `POST /api/campaigns/{id}/cancel`, `POST /api/identity/users/{userId}/roles`) fail to declare `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]` or `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]`.
- Domain conflict scenarios (e.g. attempting to publish an unapproved campaign or registering an existing email) throw exceptions resulting in error responses that are not documented in the API contract.

### Issue C: State Conflict Exceptions Mapped to 400 Bad Request Instead of 409 Conflict
In REST semantics (RFC 9110 §15.5.10):
- Registering an account with an email that is already registered is a resource conflict.
- Attempting to publish a campaign that is already published or cancelled is a state machine conflict.
- In both cases, handlers throw `InvalidOperationException`, which `GlobalExceptionHandler` translates to `400 Bad Request` rather than `409 Conflict`. Clients are unable to distinguish between malformed user inputs (400) and resource state conflicts (409).

## 2. Blast Radius
- API consumers using OpenAPI specifications receive incomplete schema definitions and cannot auto-generate error handling code for 401, 403, and 409 responses.
- Public attack surface includes superfluous, untested template endpoints.
- Clients receiving 400 for duplicate email registration treat it as a syntax/formatting error instead of prompting the user to log in or reset their password.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Controllers/WeatherForecastController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/WeatherForecastController.cs)
- [`src/API/CrowdFunding.API/WeatherForecast.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/WeatherForecast.cs)
- [`src/API/CrowdFunding.API/Controllers/IdentityController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/IdentityController.cs)
- [`src/API/CrowdFunding.API/Controllers/CampaignsController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/CampaignsController.cs)
- [`src/API/CrowdFunding.API/Controllers/ContributionsController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/ContributionsController.cs)
- [`src/API/CrowdFunding.API/Controllers/ModerationController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/ModerationController.cs)

## 4. Recommended Fix (Greenfield)
1. **Delete template files:** Remove `WeatherForecastController.cs` and `WeatherForecast.cs`.
2. **Add standard error response conventions:** Apply OpenAPI attributes across all controller endpoints:
   ```csharp
   [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
   [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
   ```
3. **Introduce a dedicated `ResourceConflictException`:**
   Throw `ResourceConflictException` (or `DuplicateResourceException`) for duplicate email registration and invalid state machine transitions, and map it in `GlobalExceptionHandler` to `409 Conflict`.
