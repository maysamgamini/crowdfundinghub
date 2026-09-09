# QA Ticket: TICKET-047

**Title:** REST Contract Incompleteness: Missing GET/DELETE Endpoints & 404 Location Headers for Reward Tiers & Webhook Subscriptions  
**Severity:** 🟡 P2 (Medium - API Contract Compliance, REST Semantics & RFC 9110)  
**QA Focus Area:** API Contract Compliance & RESTful Semantics  
**Found By:** `qa-api-contract-compliance`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

Under RFC 9110 §10.2.2 and REST architectural constraints:
When an HTTP endpoint creates a resource and returns `201 Created`, it MUST provide a valid URI in the `Location` response header that allows clients to immediately `GET` the created resource representation.

Inspecting two recently added controllers reveals that both violate this fundamental REST contract:

### Case A: Reward Tiers Controller
In [`RewardTiersController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/RewardTiersController.cs#L69):
```csharp
[HttpPost]
public async Task<ActionResult<CreateRewardTierResponse>> Create(...)
{
    // ...
    return Created($"/api/campaigns/{campaignId}/reward-tiers/{response.RewardTierId}", response);
}
```
However, there is **NO `GET /api/campaigns/{campaignId}/reward-tiers/{rewardTierId}` endpoint**!
Furthermore, there is **NO `GET /api/campaigns/{campaignId}/reward-tiers` endpoint** for backers to query available reward tiers for a campaign! Following the `Location` header results in an immediate **`404 Not Found`**.

### Case B: Campaign Webhook Subscriptions Controller
In [`CampaignWebhookSubscriptionsController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/CampaignWebhookSubscriptionsController.cs#L67):
```csharp
[HttpPost]
public async Task<ActionResult<RegisterWebhookSubscriptionResponse>> Register(...)
{
    // ...
    return Created($"/api/campaigns/{campaignId}/webhook-subscriptions/{response.SubscriptionId}", response);
}
```
However, there is **NO `GET /api/campaigns/{campaignId}/webhook-subscriptions/{subscriptionId}` endpoint**, NO `GET` list endpoint, and NO `DELETE` endpoint to cancel/disable a subscription! Following the `Location` header results in an immediate **`404 Not Found`**.

---

## 2. Blast Radius & Client Impact

- **Broken REST Client Automations:** Standard REST SDKs and hypermedia navigators follow `201 Created` Location headers to fetch the created resource. In both cases, client code crashes with `404 Not Found`.
- **Inaccessible Reward Perks:** Backers browsing a campaign on the frontend have no API query to fetch available reward perk tiers, pricing, or remaining inventory.
- **Unmanageable Webhooks:** Once registered, a creator has no mechanism to list their active webhook subscriptions or delete a subscription if their destination server changes or is compromised.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects why **Every 201 Created Response Requires a Corresponding GET Query Handler**. Returning a resource URI in a `Location` header is a formal contract with the HTTP client. If an endpoint advertises a resource URI, the backend must implement the read side of that URI.

### Monolith First, Microservices Ready
Clean CQRS separates commands from queries, but clean API design requires symmetry. Providing queries alongside commands ensures that external consumers and internal client applications have complete resource lifecycle access.

---

## 4. Affected Files & Modules

- [`src/API/CrowdFunding.API/Controllers/RewardTiersController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/RewardTiersController.cs)
- [`src/API/CrowdFunding.API/Controllers/CampaignWebhookSubscriptionsController.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/API/CrowdFunding.API/Controllers/CampaignWebhookSubscriptionsController.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/)
- [`src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Application/`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/CampaignUpdates/CrowdFunding.Modules.CampaignUpdates.Application/)

---

## 5. Greenfield Remediation Guidance

1. Implement `GetRewardTiersByCampaignIdQuery` and `GetRewardTierByIdQuery` in Campaigns module, and expose:
   - `GET /api/campaigns/{campaignId}/reward-tiers`
   - `GET /api/campaigns/{campaignId}/reward-tiers/{rewardTierId}`
2. Implement `GetWebhookSubscriptionsByCampaignIdQuery` and `DeleteWebhookSubscriptionCommand` in CampaignUpdates module, and expose:
   - `GET /api/campaigns/{campaignId}/webhook-subscriptions`
   - `GET /api/campaigns/{campaignId}/webhook-subscriptions/{subscriptionId}`
   - `DELETE /api/campaigns/{campaignId}/webhook-subscriptions/{subscriptionId}`
3. Use `CreatedAtAction(nameof(GetById), ...)` so the `Location` header is generated from the route collection rather than a hardcoded string.

---

## 6. Verification & Acceptance Criteria

1. **Location Resolution Test:** Executing `POST` on both endpoints followed by `GET` on the returned `Location` header returns `200 OK` with the matching resource body.
2. **List Endpoints Test:** `GET /api/campaigns/{id}/reward-tiers` lists all tiers with remaining capacities.
