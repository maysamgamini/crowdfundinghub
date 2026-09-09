# QA Ticket: TICKET-051

**Title:** Reward Tier Claim Orphaned on Campaign Failure or Cancellation: `ClaimedCount` Never Decremented and Confirmed Reservation Unreleasable  
**Severity:** 🔴 P1 (High - Data Integrity, Aggregate Invariants & Compensation Saga Incompleteness)  
**QA Focus Area:** Domain Aggregate Lifecycle, Distributed Sagas & Compensation Choreography  
**Found By:** `qa-domain-verification` & `qa-resilience-outbox`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In [TICKET-034](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/qa-tickets/TICKET-034-REWARD-TIER-ALLOCATION-INVENTORY-RESERVATION.md) and [TICKET-043](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/qa-tickets/TICKET-043-REWARD-TIER-UNWIRED-CLAIM-CONFIRMATION-AND-EXPIRATION.md), reward tier allocation was implemented via a two-phase reservation pattern:
1. `RewardTierReservation` holds a slot during checkout in status `Reserved`.
2. When payment succeeds, [`AddContributionToCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/AddContributionToCampaign/AddContributionToCampaignCommandHandler.cs#L145-L178) consumes [`ContributionPaymentConfirmedApplicationEvent`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionPaymentConfirmed/ContributionPaymentConfirmedApplicationEvent.cs), invokes `reservation.Confirm(contributionId)`, and executes [`tier.ConfirmClaim()`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs#L107-L116) (which decrements `ReservedCount` and increments `ClaimedCount`).

### The Architectural Defect

When a campaign fails to hit its funding goal by deadline, or is cancelled by an administrator, the refund saga kicks off in [`CampaignTerminationRefundHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Events/CampaignTerminationRefundHandler.cs#L45-L62):
```csharp
foreach (var contribution in succeededContributions)
{
    contribution.Refund(_dateTimeProvider.UtcNow);
    await _contributionRepository.UpdateAsync(contribution, ct);
}
```
Each refunded contribution publishes a [`ContributionRefundedApplicationEvent`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionRefunded/ContributionRefundedApplicationEvent.cs).

However:
1. **Missing Event Subscription:** The `Campaigns` module does **not** have an event handler listening to `ContributionRefundedApplicationEvent`.
2. **Missing Aggregate Domain Method:** In [`RewardTier.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs), there is `ReserveSlot()`, `ReleaseReservation()`, and `ConfirmClaim()`, but **no method to unclaim or refund a claim** (`RefundClaim()` or `ReleaseClaim()`).
3. **Invalid State Transition on Reservation Aggregate:** In [`RewardTierReservation.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTierReservation.cs#L103-L111):
   ```csharp
   public void Release()
   {
       if (Status != RewardTierReservationStatus.Reserved)
       {
           throw new InvalidOperationException($"Cannot release reservation '{Id}' in status '{Status}'.");
       }
       Status = RewardTierReservationStatus.Released;
   }
   ```
   If a reservation has already been confirmed (`Status == RewardTierReservationStatus.Confirmed`), calling `Release()` throws `InvalidOperationException`! There is no `Refund()` or `ReleaseConfirmed()` state transition.

---

## 2. Blast Radius & Impact Analysis

- **Permanent Inventory Corruption:** When a campaign is cancelled or fails, all backers are refunded their money, but the campaign's `RewardTier.ClaimedCount` remains permanently incremented.
- **Reporting & Financial Desynchronization:** Aggregate tier analytics report items as "Sold Out" and "Claimed", even though $0.00 remains in escrow and all backers have been refunded.
- **Inability to Reopen or Relaunch Campaigns:** If a creator resolves a moderation flag on a cancelled campaign and seeks to reopen it, perk capacity is permanently exhausted by ghost claims.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach developers and architects the **Symmetric Compensation Principle** in Distributed Sagas:
> *For every forward state transition in an aggregate (`Reserve` -> `Confirm`), there must exist an equivalent, idempotent compensating state transition (`Confirm` -> `Refunded`/`Released`) triggered by compensating events.*

When designing event-driven choreography between bounded contexts (`Contributions` and `Campaigns`), state transitions cannot be unidirectional. If `Contributions` issues a refund, `Campaigns` must complete the compensating loop by rolling back perk allocations.

---

## 4. Affected Files & Modules

- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTierReservation.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTierReservation.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/RewardTiers/Events/ContributionRefundedRewardTierHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/RewardTiers/) *(New Handler)*
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionRefunded/ContributionRefundedApplicationEvent.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionRefunded/ContributionRefundedApplicationEvent.cs)

---

## 5. Greenfield Remediation Guidance

### Step 1: Add Domain Methods to `RewardTier` and `RewardTierReservation`

1. In [`RewardTier.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs), add `ReleaseClaim()`:
   ```csharp
   /// <summary>
   /// Reverses a confirmed claim when a contribution is refunded during campaign failure or cancellation.
   /// </summary>
   public void ReleaseClaim()
   {
       if (ClaimedCount <= 0)
       {
           throw new InvalidOperationException("No claimed slot to release for this tier.");
       }

       ClaimedCount--;
   }
   ```

2. In [`RewardTierReservation.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTierReservation.cs), add support for refunding confirmed reservations:
   ```csharp
   public void Refund()
   {
       if (Status != RewardTierReservationStatus.Confirmed)
       {
           throw new InvalidOperationException($"Cannot refund reservation '{Id}' in status '{Status}'.");
       }

       Status = RewardTierReservationStatus.Released;
   }
   ```

### Step 2: Thread `RewardTierReservationId` Through `ContributionRefundedApplicationEvent`

Update `ContributionRefundedApplicationEvent` to include the nullable `Guid? RewardTierReservationId`.

### Step 3: Implement `ContributionRefundedRewardTierHandler` in `Campaigns`

Implement an event handler listening to `ContributionRefundedApplicationEvent`:
1. If `notification.RewardTierReservationId` is null, exit cleanly (generic contribution without perk).
2. Fetch the `RewardTierReservation` by id.
3. If already `Released`, return idempotently.
4. Call `reservation.Refund()`.
5. Fetch the parent `RewardTier` and call `tier.ReleaseClaim()`.
6. Save changes within an advisory lock keyed to the `RewardTier.Id`.

---

## 6. Verification & Acceptance Criteria

1. **Refund Saga Decrements Counter:** When a campaign is cancelled or fails, all refunded contributions linked to a reward tier cause `RewardTier.ClaimedCount` to decrement back to 0.
2. **Idempotency:** Re-processing the same `ContributionRefundedApplicationEvent` does not double-decrement `ClaimedCount` or throw an exception.
3. **Availability Restored:** `RewardTier.AvailableCount` accurately reflects refunded capacity.
