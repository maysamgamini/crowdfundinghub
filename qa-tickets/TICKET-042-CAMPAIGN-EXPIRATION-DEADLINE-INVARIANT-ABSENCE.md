# QA Ticket: TICKET-042

**Title:** Campaign Expiration State Machine Invariants: Missing Past-Deadline Verification on `Complete` and `Fail` Commands  
**Severity:** 🔴 P1 (Critical - Business Domain Invariants & Premature Campaign Termination)  
**QA Focus Area:** Domain Aggregate Lifecycle, Business Invariants & Command Validation  
**Found By:** `qa-domain-verification`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In crowdfunding domain economics (Kickstarter / Indiegogo standard), a campaign has an explicit temporal boundary: `DeadlineUtc`. The campaign promises creators that they have until `DeadlineUtc` to raise funds, and backers are permitted to pledge until that exact timestamp.

In [`Campaign.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs#L127-L164), two termination methods were introduced for TICKET-027:
- `CompleteSuccessfully(DateTime nowUtc)`
- `MarkFailed(DateTime nowUtc)`

Inspecting their implementations reveals a critical domain invariant omission:
```csharp
public void CompleteSuccessfully(DateTime nowUtc)
{
    if (Status != CampaignStatus.Published)
    {
        throw new InvalidOperationException($"Only published campaigns can be completed. Current status: '{Status}'.");
    }

    if (RaisedAmount.Amount < GoalAmount.Amount)
    {
        throw new InvalidOperationException("Cannot complete a campaign successfully — its funding goal was not reached.");
    }

    // MISSING INVARIANT CHECK: Does NOT verify that DeadlineUtc <= nowUtc!
    Status = CampaignStatus.Successful;
    AddDomainEvent(new CampaignSucceededDomainEvent(Id, OwnerId, RaisedAmount.Amount, RaisedAmount.Currency, nowUtc));
}

public void MarkFailed(DateTime nowUtc)
{
    if (Status != CampaignStatus.Published)
    {
        throw new InvalidOperationException($"Only published campaigns can be marked failed. Current status: '{Status}'.");
    }

    if (RaisedAmount.Amount >= GoalAmount.Amount)
    {
        throw new InvalidOperationException("Cannot mark a campaign failed — its funding goal was reached.");
    }

    // MISSING INVARIANT CHECK: Does NOT verify that DeadlineUtc <= nowUtc!
    Status = CampaignStatus.Failed;
    AddDomainEvent(new CampaignFailedDomainEvent(Id, OwnerId, RaisedAmount.Amount, GoalAmount.Amount, RaisedAmount.Currency, nowUtc));
}
```

Furthermore, [`CompleteCampaignCommand`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CompleteCampaign/CompleteCampaignCommand.cs) and [`FailCampaignCommand`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/FailCampaign/FailCampaignCommand.cs) have **zero FluentValidation validators** and their handlers do not perform any deadline validation.

---

## 2. Blast Radius & Business Domain Violation

1. **Premature Campaign Failure (Backer Lockout):** If `FailCampaignCommand` is dispatched early (e.g. by an operator script, misconfigured scheduler, or administrative event), a campaign that is on day 5 of a 30-day funding window with 25 days remaining will be marked `Failed`. This triggers [`CampaignTerminationRefundHandler`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs), refunding all contributions and irreversibly destroying the creator's live campaign.
2. **Premature Completion (Stretch Goal Denial):** If `CompleteSuccessfully` is called as soon as the base funding goal is hit, the campaign status transitions to `Successful`. Once in `Successful`, [`Campaign.ApplyConfirmedContribution`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs#L172) rejects all further pledges (`"Only Published campaigns can accept funds"`). This cuts off backers and prevents the creator from raising additional stretch goal funding before the official deadline.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects that **Domain Aggregates Must Be Self-Contained Invariant Guardians**. An aggregate must NEVER rely on external callers (such as a background poller query `WHERE deadline_utc <= now`) to protect its core business rules. If a method transition is invalid before a specific point in time, the aggregate root itself must enforce that temporal invariant (`nowUtc >= DeadlineUtc`).

### Monolith First, Microservices Ready
When the Campaigns module is separated into its own service, multiple consumers (background services, manual administrative overrides, webhook handlers) might invoke completion or failure endpoints. The aggregate boundary must reject premature terminations deterministically, independent of the transport or caller.

### What Breaks Tomorrow If Ignored Today?
Any rogue administrative action, automated scheduler glitch, or clock skew bug can terminate live campaigns weeks before their scheduled finish date, causing irreversible reputational and financial damage.

---

## 4. Affected Files & Modules

- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/Campaign.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CompleteCampaign/CompleteCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/CompleteCampaign/CompleteCampaignCommandHandler.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/FailCampaign/FailCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/FailCampaign/FailCampaignCommandHandler.cs)

---

## 5. Greenfield Remediation Guidance

1. In `Campaign.CompleteSuccessfully(DateTime nowUtc)`:
   ```csharp
   if (nowUtc < DeadlineUtc)
   {
       throw new InvalidOperationException(
           $"Cannot complete campaign '{Id}' before its deadline '{DeadlineUtc:O}'. Current time: '{nowUtc:O}'.");
   }
   ```
2. In `Campaign.MarkFailed(DateTime nowUtc)`:
   ```csharp
   if (nowUtc < DeadlineUtc)
   {
       throw new InvalidOperationException(
           $"Cannot mark campaign '{Id}' failed before its deadline '{DeadlineUtc:O}'. Current time: '{nowUtc:O}'.");
   }
   ```
3. Add `CompleteCampaignCommandValidator` and `FailCampaignCommandValidator` enforcing non-empty `CampaignId`.

---

## 6. Verification & Acceptance Criteria

1. **Domain Unit Tests:** Calling `campaign.CompleteSuccessfully(earlyDate)` or `campaign.MarkFailed(earlyDate)` where `earlyDate < DeadlineUtc` throws `InvalidOperationException`.
2. **Valid Post-Deadline Resolution:** Calling both methods when `nowUtc >= DeadlineUtc` transitions the status and emits the respective domain event successfully.
