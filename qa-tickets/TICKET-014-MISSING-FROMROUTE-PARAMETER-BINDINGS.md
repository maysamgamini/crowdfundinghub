# QA Ticket: TICKET-014

**Title:** Missing Explicit `[FromRoute]` Parameter Binding Attributes Across All API Controllers  
**Severity:** 🟡 P2 (Medium - API Contract & OpenAPI Schema Fidelity)  
**QA Focus Area:** API Contracts & Route Parameter Binding  
**Found By:** `qa-api-contracts`  
**Status:** Fixed  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
Across all four production API controllers in `src/API/CrowdFunding.API/Controllers/`, route parameters in action method signatures lack the explicit `[FromRoute]` attribute.

While ASP.NET Core MVC model binding can infer route parameter bindings when the parameter name matches a route segment token, omitting explicit `[FromRoute]` introduces significant issues:
1. **OpenAPI / Swagger Generation Fragility:** Swashbuckle and OpenAPI document generators rely on metadata attributes to accurately categorize parameter locations (`in: "path"` vs `in: "query"`). Without explicit attributes, tooling can produce degraded or incorrect OpenAPI operation definitions, especially when custom model binders or filters are introduced.
2. **Client SDK Code Generation Bugs:** Code generators (such as OpenAPI Generator, NSwag, or Orval) generating TypeScript/C# client libraries frequently misclassify parameters whose binding source is implicit, leading to client-side calls that append route values as query string parameters or body properties.
3. **Contract Asymmetry:** Controllers rigorously annotate `[FromBody]` and `[FromQuery]` for payloads and filter parameters, but leave route bindings implicit, violating clean coding guidelines and explicit design principles.

## 2. Affected Action Methods
The following 12 action parameters across 4 controllers are missing `[FromRoute]`:

1. **`src/API/CrowdFunding.API/Controllers/CampaignsController.cs`**:
   - `Publish(Guid id, ...)` -> line 104
   - `Cancel(Guid id, ...)` -> line 124
   - `GetById(Guid id, ...)` -> line 142
2. **`src/API/CrowdFunding.API/Controllers/ContributionsController.cs`**:
   - `ListByCampaign(Guid campaignId, ...)` -> line 52
   - `Create(Guid campaignId, ...)` -> line 84
   - `ConfirmPayment(Guid campaignId, Guid contributionId, ...)` -> lines 109-110
   - `FailPayment(Guid campaignId, Guid contributionId, ...)` -> lines 133-134
3. **`src/API/CrowdFunding.API/Controllers/IdentityController.cs`**:
   - `AssignRole(Guid userId, ...)` -> line 109
   - `GrantPermission(Guid userId, ...)` -> line 131
4. **`src/API/CrowdFunding.API/Controllers/ModerationController.cs`**:
   - `GetByCampaignId(Guid campaignId, ...)` -> line 44
   - `Approve(Guid campaignId, ...)` -> line 59
   - `Reject(Guid campaignId, ...)` -> line 87

## 3. Recommended Fix (Greenfield)
Update all controller action signatures to explicitly declare `[FromRoute]` on all route path parameters:

```csharp
// Example in CampaignsController:
[HttpPost("{id:guid}/publish")]
public async Task<ActionResult<PublishCampaignResponse>> Publish(
    [FromRoute] Guid id,
    CancellationToken cancellationToken)

// Example in ContributionsController:
[HttpPost("{contributionId:guid}/confirm-payment")]
public async Task<ActionResult<ConfirmContributionPaymentResponse>> ConfirmPayment(
    [FromRoute] Guid campaignId,
    [FromRoute] Guid contributionId,
    [FromBody] ConfirmContributionPaymentRequest request,
    CancellationToken cancellationToken)
```

---

## Resolution Note (doc reconciliation pass)

This ticket's fix already landed in commit `bdfb810` earlier in this session's branch history; the `Status` field above was not updated at the time. Verified against current code during the TICKET-036/037/039 follow-up audit (2026-09-08) — the described defect no longer reproduces.
