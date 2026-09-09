# QA Ticket: TICKET-054

**Title:** Missing Creator Self-Pledge Fraud Prevention Invariant: Creators Can Back Their Own Campaigns & Wash Funds  
**Severity:** 🟠 P1 (High - Financial Integrity, Fraud Prevention & AML Compliance)  
**QA Focus Area:** Financial Domain Invariants, Fraud Prevention & Event-Carried State Transfer  
**Found By:** `qa-security-pentest` & `qa-functional-domain`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In modern crowdfunding platforms (Kickstarter, Indiegogo, GoFundMe), creators are strictly prohibited from pledging money to their own campaigns. This restriction is mandated by Anti-Money Laundering (AML) regulations, payment processor terms of service (e.g. Stripe Services Agreement), and platform integrity rules.

### The Architectural Defect

In [`MakeContributionCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs#L41-L75):

```csharp
var campaign = await _activeCampaignCacheRepository.GetAsync(command.CampaignId, cancellationToken);

if (campaign is null)
{
    throw new KeyNotFoundException($"Campaign with id '{command.CampaignId}' was not found.");
}

if (!campaign.IsActive || campaign.DeadlineUtc <= _dateTimeProvider.UtcNow)
{
    throw new InvalidOperationException(
        $"Campaign '{command.CampaignId}' cannot accept contributions — it is not active or has passed its deadline.");
}

if (!string.Equals(campaign.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Contribution currency '{command.Currency}' does not match campaign currency '{campaign.Currency}'.");
}

var contribution = Contribution.Create(
    command.CampaignId,
    _currentUser.UserId,
    command.Amount,
    command.Currency,
    _dateTimeProvider.UtcNow,
    command.RewardTierReservationId);
```

Notice:
1. In [`ActiveCampaignCache.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/ReadModels/ActiveCampaignCache.cs#L9-L17), the replicated read model only contains `CampaignId`, `Title`, `Currency`, `IsActive`, `DeadlineUtc`, and `UpdatedAtUtc`. **It does not store `OwnerId` / `CreatorId`**.
2. Even though both [`CampaignCreatedApplicationEvent`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Events/CampaignCreated/CampaignCreatedApplicationEvent.cs#L20) and [`CampaignPublishedApplicationEvent`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Contracts/Events/CampaignPublished/CampaignPublishedApplicationEvent.cs#L17) carry `OwnerId`, [`ReplicatedCampaignEventHandlers.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/ActiveCampaigns/Events/ReplicatedCampaignEventHandlers.cs) discards the owner information.
3. Neither `MakeContributionCommandHandler` nor `Contribution.Create` verifies whether `_currentUser.UserId == campaign.OwnerId`.

---

## 2. Blast Radius & Impact Analysis

- **Self-Pledge & Circular Fund Washing:** A malicious creator can create a campaign, pledge their own funds using stolen credit cards or fraudulent lines of credit, reach the campaign threshold, and collect the platform payout minus processing fees.
- **Artificial Social Proof & Algorithmic Manipulation:** Creators can fabricate high backer counts and artificial momentum to push their campaigns to platform trending lists.
- **Perk Tier Hoarding:** Creators can reserve and claim their own limited reward perk tiers, locking out authentic community backers.
- **Regulatory Non-Compliance:** Violates PCI DSS, FINCEN, and payment processor Anti-Money Laundering (AML) merchant agreements.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects how **Event-Carried State Transfer (ECST) Must Capture Necessary Domain Authorization Attributes**:
When decomposing bounded contexts to avoid synchronous module calls (as implemented in TICKET-023), the replicated projection (`ActiveCampaignCache`) must not merely replicate operational data (currency, deadline); it must also replicate **ownership and identity context** required to enforce authorization and anti-fraud business invariants at the command boundary.

---

## 4. Affected Files & Modules

- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/ReadModels/ActiveCampaignCache.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/ReadModels/ActiveCampaignCache.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Repositories/ActiveCampaignCacheRepository.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/Repositories/ActiveCampaignCacheRepository.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/ActiveCampaigns/Events/ReplicatedCampaignEventHandlers.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/ActiveCampaigns/Events/ReplicatedCampaignEventHandlers.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs)

---

## 5. Greenfield Remediation Guidance

### Step 1: Add `OwnerId` to `ActiveCampaignCache`

In [`ActiveCampaignCache.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Infrastructure/Persistence/ReadModels/ActiveCampaignCache.cs):
```csharp
public Guid OwnerId { get; set; }
```

### Step 2: Update `ActiveCampaignCacheRepository` and `ReplicatedCampaignEventHandlers`

Update `UpsertAsync` to accept and persist `OwnerId` from `CampaignCreatedApplicationEvent.OwnerId`.

### Step 3: Enforce Invariant in `MakeContributionCommandHandler`

In [`MakeContributionCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/MakeContribution/MakeContributionCommandHandler.cs):

```csharp
if (campaign.OwnerId == _currentUser.UserId)
{
    throw new InvalidOperationException("Campaign creators cannot back or contribute to their own campaigns.");
}
```

---

## 6. Verification & Acceptance Criteria

1. **Creator Self-Pledge Rejected:** Attempting to call `POST /api/campaigns/{id}/contributions` as the campaign creator returns `400 Bad Request` or `422 Unprocessable Entity` with an RFC 9457 Problem Details response.
2. **Replicated Read Model Stores `OwnerId`:** Database projection `contributions.active_campaign_cache` records the `owner_id` column for all created campaigns.
3. **Legitimate Backers Unaffected:** Non-creator users can successfully contribute without regression.
