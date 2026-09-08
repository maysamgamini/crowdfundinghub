# QA Ticket: TICKET-006

**Title:** Missing Single-Resource `GET` Contribution Endpoint and Non-Standard `Location` Header on Creation  
**Severity:** 🟡 P2 (Medium - API Contract & REST Standards)  
**QA Focus Area:** API Contracts & Architecture QA  
**Found By:** `qa-api-contracts`  
**Status:** Open  
**Project Mode:** Greenfield (No backward compatibility required)  

---

## 1. Description
In `ContributionsController.cs`, when a new contribution is created:

```csharp
[Authorize(Policy = PermissionConstants.CampaignsContribute)]
[EnableRateLimiting(RateLimitingConfiguration.PaymentPolicy)]
[HttpPost]
[ProducesResponseType(typeof(MakeContributionResponse), StatusCodes.Status201Created)]
public async Task<ActionResult<MakeContributionResponse>> Create(
    Guid campaignId,
    [FromBody] MakeContributionRequest request,
    CancellationToken cancellationToken)
{
    ...
    var result = await _commandDispatcher.SendAsync<MakeContributionResult>(command, cancellationToken);
    var response = _mapper.Map<MakeContributionResponse>(result);

    return CreatedAtAction(nameof(ListByCampaign), new { campaignId }, response);
}
```

Issues:
1. `CreatedAtAction(nameof(ListByCampaign), ...)` sets the HTTP `Location` response header to the collection endpoint (`/api/campaigns/{campaignId}/contributions`) rather than the individual newly created resource.
2. Under REST guidelines and RFC 9110 §10.3.2, a `201 Created` response's `Location` header must refer to the primary resource created (i.e. `/api/campaigns/{campaignId}/contributions/{contributionId}`).
3. There is **no** `GET /api/campaigns/{campaignId}/contributions/{contributionId}` endpoint anywhere in `ContributionsController` or the `Contributions` module. A client receiving a `MakeContributionResponse` has no way to fetch that specific contribution's details or status directly.

## 2. Blast Radius & Defect Reproduction
1. Client makes a contribution: receives `201 Created` with `Location: /api/campaigns/{campaignId}/contributions`.
2. Follow-up automated API clients following the `Location` header receive a paginated list of all contributions rather than the created contribution.
3. If a client needs to poll the contribution payment status (`Pending` -> `Succeeded`), they are forced to load and filter the entire campaign contributions list.

## 3. Affected Files
- [`src/API/CrowdFunding.API/Controllers/ContributionsController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/ContributionsController.cs#L80-L101)
- `src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/` (missing GetContributionById query/handler)

## 4. Recommended Fix (Greenfield)
1. Implement `GetContributionByIdQuery` and handler in `Contributions.Application`.
2. Add `GET /api/campaigns/{campaignId}/contributions/{contributionId:guid}` endpoint in `ContributionsController`:
   ```csharp
   [HttpGet("{contributionId:guid}")]
   [ProducesResponseType(typeof(GetContributionByIdResponse), StatusCodes.Status200OK)]
   [ProducesResponseType(StatusCodes.Status404NotFound)]
   public async Task<ActionResult<GetContributionByIdResponse>> GetById(
       Guid campaignId,
       Guid contributionId,
       CancellationToken cancellationToken)
   {
       ...
   }
   ```
3. Update `ContributionsController.Create` to return:
   ```csharp
   return CreatedAtAction(nameof(GetById), new { campaignId, contributionId = response.ContributionId }, response);
   ```
