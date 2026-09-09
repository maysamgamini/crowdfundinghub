# QA Ticket: TICKET-052

**Title:** Immediate Slot Lockout on Payment Failure: `Contribution.FailPayment` Emits Zero Domain Events Leaving Reward Reservation Locked for 15 Minutes  
**Severity:** 🟠 P1 (High - Inventory Lockout & False Sold-Out State)  
**QA Focus Area:** Financial State Machine, Outbox Domain Events & Inventory Reservation Sagas  
**Found By:** `qa-concurrency-audit` & `qa-domain-verification`  
**Status:** Fixed  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

When a backer attempts to back a campaign with a limited perk tier:
1. An uncommitted hold is acquired via `ReserveRewardTierSlotCommandHandler`, generating a `RewardTierReservation` with a 15-minute expiration window (`ReservationWindow = TimeSpan.FromMinutes(15)`).
2. The backer submits payment via `MakeContributionCommand` and the system attempts to confirm payment with the payment gateway (Stripe / PSP).

### The Architectural Defect

If the payment fails (credit card declined, insufficient funds, fraud check failure, or Stripe `payment_intent.payment_failed` webhook processed by [`ReconcilePaymentWebhookCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Application/Features/Contributions/Commands/ReconcilePaymentWebhook/ReconcilePaymentWebhookCommandHandler.cs)), the system invokes [`Contribution.FailPayment`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs#L151-L167):

```csharp
public void FailPayment(string failureReason, DateTime processedAtUtc)
{
    if (Status != ContributionStatus.Pending)
    {
        throw new InvalidOperationException("Only pending contributions can be failed.");
    }

    if (string.IsNullOrWhiteSpace(failureReason))
    {
        throw new ArgumentException("Failure reason is required.", nameof(failureReason));
    }

    Status = ContributionStatus.Failed;
    PaymentReference = null;
    FailureReason = failureReason.Trim();
    ProcessedAtUtc = processedAtUtc;
}
```

Notice that while [`ConfirmPayment`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs#L142-L144) raises `ContributionPaymentConfirmedDomainEvent`, `FailPayment` **raises zero domain events**.

Consequently:
- Neither the in-process dispatcher nor the transactional outbox produces an event notifying `Campaigns` of the payment failure.
- The associated `RewardTierReservation` remains in `RewardTierReservationStatus.Reserved` state for the **entire duration of the 15-minute reservation window**.
- The `RewardTier.ReservedCount` remains incremented, reducing `AvailableCount`.

---

## 2. Blast Radius & Impact Analysis

- **Denial of Inventory / False "Sold Out" State:** In flash crowdfunding campaigns with scarce tiers (e.g. 10 "Super Early Bird" hardware prototypes), a few backers with failing cards cause the tier to immediately report as "Sold Out" (`AvailableCount == 0`). Legitimate backers with valid payment methods are turned away for 15 minutes.
- **Conversion Rate Degradation:** Prospective backers seeing "Sold Out" abandon the checkout page rather than waiting for background scavenger sweeps.
- **Asymmetric Saga Design:** The state machine handles the happy path (`Pending` -> `Succeeded` -> `Confirmed Claim`) instantly in real time, but relegates the standard negative path (`Pending` -> `Failed` -> `Release Reservation`) to a delayed background garbage collection process.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects why **Explicit Rejection Events Are As Critical As Confirmation Events** in Two-Phase Inventory Reservations.

In e-commerce and crowdfunding:
- An inventory hold is a scarce resource held at high cost.
- As soon as a definitive negative terminal state (`Failed`, `Cancelled`, `Declined`) is reached, the holding context must immediately broadcast a release event to reclaim the inventory.
- Background scavengers are designed as a **safety net for abandoned sessions**, NOT as the primary handler for known payment rejections.

---

## 4. Affected Files & Modules

- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Events/ContributionPaymentFailedDomainEvent.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Events/) *(New Domain Event)*
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionPaymentFailed/ContributionPaymentFailedApplicationEvent.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/) *(New Contract Event)*
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/RewardTiers/Events/ContributionPaymentFailedRewardTierHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/RewardTiers/) *(New Handler)*

---

## 5. Greenfield Remediation Guidance

### Step 1: Emit Domain Event in `Contribution.FailPayment`

```csharp
public void FailPayment(string failureReason, DateTime processedAtUtc)
{
    // ... validation ...
    Status = ContributionStatus.Failed;
    PaymentReference = null;
    FailureReason = failureReason.Trim();
    ProcessedAtUtc = processedAtUtc;

    AddDomainEvent(new ContributionPaymentFailedDomainEvent(
        Id, CampaignId, ContributorId, Money.Amount, Money.Currency, FailureReason, RewardTierReservationId));
}
```

### Step 2: Map to Application Event in `ContributionTransactionExecutor`

Map `ContributionPaymentFailedDomainEvent` to `ContributionPaymentFailedApplicationEvent` so it is captured by the outbox.

### Step 3: Handle Event in `Campaigns` Module

Create `ContributionPaymentFailedRewardTierHandler`:
```csharp
public async Task Handle(ContributionPaymentFailedApplicationEvent notification, CancellationToken ct)
{
    if (notification.RewardTierReservationId is null)
    {
        return;
    }

    var reservation = await _reservationRepository.GetByIdAsync(notification.RewardTierReservationId.Value, ct);
    if (reservation is null || reservation.Status != RewardTierReservationStatus.Reserved)
    {
        return;
    }

    await _transactionExecutor.ExecuteAsync(async txnCt =>
    {
        reservation.Release();
        var tier = await _rewardTierRepository.GetByIdAsync(reservation.RewardTierId, txnCt);
        tier?.ReleaseReservation();
    }, reservation.RewardTierId.GetHashCode(), ct);
}
```

---

## 6. Verification & Acceptance Criteria

1. **Immediate Release on Decline:** When a contribution payment fails via API or PSP webhook, the linked `RewardTierReservation` immediately transitions to `Released` within the same event dispatch cycle (< 50ms), rather than waiting up to 15 minutes for the scavenger.
2. **Available Count Increments:** `RewardTier.AvailableCount` immediately reflects the released slot.
3. **Audit Trail:** Outbox log confirms `ContributionPaymentFailedApplicationEvent` delivery.
