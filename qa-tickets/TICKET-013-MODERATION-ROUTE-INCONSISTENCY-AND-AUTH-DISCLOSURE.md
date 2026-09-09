# QA Ticket: TICKET-013

**Title:** Moderation API Route Inconsistency, Missing Review Listing Queue, and Unauthenticated Review Disclosure  
**Severity:** 🟠 P1 (High - Security / Information Disclosure & API Usability)  
**QA Focus Area:** API Contracts & Security QA  
**Found By:** `qa-api-contracts`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
An audit of `src/API/CrowdFunding.API/Controllers/ModerationController.cs` and the `Moderation` module revealed three significant contract and security flaws:

### Issue A: Information Disclosure — `GET /api/moderation/campaigns/{campaignId}` Lacks `[Authorize]`
In `ModerationController.cs`:
```csharp
[HttpGet("{campaignId:guid}")]
[ProducesResponseType(typeof(CampaignReviewResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<ActionResult<CampaignReviewResponse>> GetByCampaignId(Guid campaignId, CancellationToken cancellationToken)
```
Unlike the `approve` and `reject` endpoints, `GetByCampaignId` has **no `[Authorize]` attribute**. Any unauthenticated public client can query any campaign ID and retrieve:
- `CampaignReviewResponse.ModeratorId` (exposing internal administrator/moderator identity GUIDs)
- `CampaignReviewResponse.Notes` (exposing confidential internal moderation review comments and feedback)
- `CampaignReviewResponse.Status` and `ReviewedAtUtc`

### Issue B: Route Inconsistency (`api/moderation/campaigns` vs `api/moderation/reviews`)
In `ModerationController.cs`, the route attribute is:
```csharp
[Route("api/moderation/campaigns")]
```
This forces endpoints to be:
- `GET /api/moderation/campaigns/{campaignId}`
- `POST /api/moderation/campaigns/{campaignId}/approve`
- `POST /api/moderation/campaigns/{campaignId}/reject`

This conflicts with standard REST resource modeling. In REST design, the primary resource of the moderation module is a **`review`**, not a campaign. The campaign resource already belongs to `/api/campaigns`. Exposing `/api/moderation/campaigns/...` creates confusing resource ownership. Furthermore, clients expecting standard review routes such as `/api/moderation/reviews/{campaignId}` receive `404 Not Found`.

### Issue C: Missing Review Queue / Listing Endpoint
The `Moderation` module contains only two queries: `GetCampaignReviewByCampaignIdQuery` and `GetCampaignReviewStatusByCampaignIdQuery`. There is **no collection endpoint** (e.g. `GET /api/moderation/reviews?status=Pending` or `GET /api/moderation/campaigns`).
As a result:
- System moderators have no way to view the queue of campaigns pending review.
- Moderators cannot discover unmoderated campaigns via the API without an out-of-band message providing the specific `campaignId`.

## 2. Blast Radius & Defect Reproduction
1. **Security:** An anonymous actor issues `GET /api/moderation/campaigns/{campaignId}` and observes internal moderator UUIDs and administrative notes without credentials.
2. **Integration:** Frontends or external clients integrating with moderation review queues fail to discover pending submissions due to the lack of a collection listing endpoint.
3. **API Contract:** Calling `/api/moderation/reviews/{campaignId}` returns 404 because of non-standard controller routing.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Controllers/ModerationController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/ModerationController.cs#L18-L52)
- `src/Modules/Moderation/CrowdFunding.Modules.Moderation.Application/` (missing list query)
- `src/Modules/Moderation/CrowdFunding.Modules.Moderation.Infrastructure/` (missing read query)

## 4. Recommended Fix (Greenfield)

### Step 1: Protect `GetByCampaignId` with Authorization
Require appropriate moderation permissions (or allow campaign owners to view their own review notes):
```csharp
[Authorize(Policy = PermissionConstants.ModerationReview)]
[HttpGet("{campaignId:guid}")]
```

### Step 2: Canonicalize and Alias Routes
Update `ModerationController` to standardize on `api/moderation/reviews` while maintaining aliasing if needed:
```csharp
[ApiController]
[Route("api/moderation/reviews")]
public sealed class ModerationController : ControllerBase
{
    [HttpGet("{campaignId:guid}")]
    ...

    [HttpPost("{campaignId:guid}/approve")]
    ...

    [HttpPost("{campaignId:guid}/reject")]
    ...
}
```

### Step 3: Implement Review Queue Listing Endpoint
1. Add `ListCampaignReviewsQuery(string? status, PageRequest pageRequest)` to `Modules/Moderation/Application`.
2. Expose `GET /api/moderation/reviews`:
```csharp
[Authorize(Policy = PermissionConstants.ModerationReview)]
[HttpGet]
[ProducesResponseType(typeof(PagedResponse<CampaignReviewResponse>), StatusCodes.Status200OK)]
public async Task<ActionResult<PagedResponse<CampaignReviewResponse>>> List(
    [FromQuery] string? status,
    [FromQuery] int? pageNumber,
    [FromQuery] int? pageSize,
    CancellationToken cancellationToken)
```

---

## Resolution Note (doc reconciliation pass)

This ticket's fix already landed in commit `bdfb810` earlier in this session's branch history; the `Status` field above was not updated at the time. Verified against current code during the TICKET-036/037/039 follow-up audit (2026-09-08) — the described defect no longer reproduces.
