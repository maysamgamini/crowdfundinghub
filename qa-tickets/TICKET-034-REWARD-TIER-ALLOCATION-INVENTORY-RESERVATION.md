# QA Ticket: TICKET-034

**Title:** Reward Perk Tier Allocation & Inventory Reservation State Machine (Rich DDD & Concurrency Defense)  
**Severity:** 🟠 P1 (High - Core Domain Feature Justifying Rich DDD & Optimistic Locking)  
**QA Focus Area:** Domain Aggregate Invariants, Optimistic Concurrency & Inventory Reservation Sagas  
**Found By:** `qa-architect-curriculum`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In the current codebase, contributions are simple arbitrary financial amounts without associated reward perks or inventory restrictions.

In commercial crowdfunding platforms (Kickstarter, Indiegogo), campaigns rely heavily on **Tiered Reward Perks** (e.g., "Early Bird Collector's Edition - Limited to 50 backers at $199").

### Why Simple CRUD Fails For Limited Rewards
When a popular creator launches a campaign:
- Hundreds of backers attempt to claim the 50 "Early Bird" slots simultaneously.
- A naive CRUD query (`UPDATE reward SET count = count + 1 WHERE id = ...`) without rich domain aggregate rules causes:
  1. **Overselling / Negative Inventory:** 53 backers are granted the 50-limit perk due to race conditions.
  2. **Orphaned Reservations:** Backers select a perk, start checkout, but abandon the cart. The inventory remains permanently locked unless an expiring reservation state machine is modeled.
  3. **Lack of Lifecycle Events:** Downstream systems (e.g. physical shipping / manufacturing microservices) are not notified when a reward perk sells out.

---

## 2. Blast Radius & Architectural Justification

- **Justifies Rich DDD:** Proves why encapsulation within an Aggregate Root (`RewardTier`) is mandatory to enforce the invariant: `ClaimedCount + ReservedCount <= TotalCapacity`.
- **Justifies PostgreSQL Concurrency Tokens:** Requires `xmin` optimistic locking or advisory locks to reject conflicting simultaneous claims.
- **Justifies Outbox Events:** Emits `RewardTierSoldOutApplicationEvent` and `PerkClaimReservedApplicationEvent` to trigger inventory reservation expiration timers.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach the **True Justification for Domain-Driven Design Aggregate Invariants & Optimistic Concurrency**. Architects learn that DDD is not about folder structures or repository boilerplate; it is about protecting critical business invariants under high concurrency.

### Monolith First, Microservices Ready
The outcome of this project is a **Modular Monolith, NOT microservices**. Inside our monolith, limited physical reward perks (e.g. 50 limited vinyl records) represent a constrained physical inventory. Multiple users check out simultaneously. By modeling `RewardTier` as an aggregate root protected by PostgreSQL `xmin` system row versioning and managing a 15-minute reservation timer via domain events, the monolith guarantees zero overselling. If inventory fulfillment is later extracted into a dedicated Supply Chain / Warehouse Microservice, **the inventory reservation domain aggregate is already fully encapsulated and emission-ready**.

### What Breaks Tomorrow If Ignored Today?
If you build reward perks with anemic CRUD entities, concurrent checkouts will oversell limited tiers, resulting in embarrassing backer cancellations, customer support crises, and manual database cleanup.

---

## 4. Affected Files & Modules

- Creation of `RewardTier` aggregate in `src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs`
- Integration with Contributions in `src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/`

---

## 4. Implementation Specification & Greenfield Solution

```mermaid
stateDiagram-v2
    [*] --> Available: Creator Configures Tier (Capacity: 50)
    Available --> InCheckout: Backer initiates pledge (ReservedCount++)
    InCheckout --> Claimed: Payment confirmed within 15m (ClaimedCount++, ReservedCount--)
    InCheckout --> Available: 15m Timer expires or Payment Cancelled (ReservedCount--)
    Claimed --> SoldOut: ClaimedCount == Capacity
    SoldOut --> [*]
```

### Step 1: Create `RewardTier` Aggregate in `Campaigns.Domain`
```csharp
namespace CrowdFunding.Modules.Campaigns.Domain.Aggregates;

public sealed class RewardTier : AggregateRoot<Guid>
{
    public Guid CampaignId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Money MinimumPledgeAmount { get; private set; }
    public int TotalCapacity { get; private set; }
    public int ClaimedCount { get; private set; }
    public int ReservedCount { get; private set; }
    public uint Version { get; private set; } // xmin optimistic concurrency token

    public int AvailableCount => TotalCapacity - (ClaimedCount + ReservedCount);

    public void ReserveSlot(DateTime now)
    {
        if (AvailableCount <= 0)
        {
            throw new InvalidOperationException($"Reward tier '{Title}' is completely sold out.");
        }

        ReservedCount++;
        AddDomainEvent(new RewardTierSlotReservedDomainEvent(Id, CampaignId, AvailableCount, now));
    }

    public void ConfirmClaim(DateTime now)
    {
        if (ReservedCount <= 0)
        {
            throw new InvalidOperationException("No reserved slot to confirm for this tier.");
        }

        ReservedCount--;
        ClaimedCount++;

        if (AvailableCount == 0)
        {
            AddDomainEvent(new RewardTierSoldOutDomainEvent(Id, CampaignId, TotalCapacity, now));
        }
    }

    public void ReleaseReservation(DateTime now)
    {
        if (ReservedCount > 0)
        {
            ReservedCount--;
            AddDomainEvent(new RewardTierSlotReleasedDomainEvent(Id, CampaignId, AvailableCount, now));
        }
    }
}
```

### Step 2: Concurrency Enforcement in EF Core
In `RewardTierConfiguration.cs`:
```csharp
builder.ToTable("reward_tiers", "campaigns");
builder.Property(r => r.Version)
    .IsRowVersion(); // Maps to PostgreSQL xmin system column
```

### Step 3: Reservation Expiration Worker
Implement `PerkReservationExpirationWorker` in `Contributions.Infrastructure`:
- Periodically checks contributions in `PendingPayment` older than 15 minutes.
- Dispatches `CancelExpiredPledgeCommand`.
- Emits `PerkReservationExpiredApplicationEvent`, prompting `RewardTier.ReleaseReservation()`.

---

## 5. Verification & Acceptance Criteria

1. **High Concurrency Stress Test:** 50 concurrent tasks attempting to reserve the last 5 slots of a reward tier results in exactly 5 successes and 45 handled concurrency/conflict rejections.
2. **Zero Overselling:** Under no load scenario does `ClaimedCount + ReservedCount` exceed `TotalCapacity`.
3. **Outbox Sold-Out Broadcast:** When the 50th slot is claimed, `RewardTierSoldOutApplicationEvent` is committed to the outbox and broadcast via SignalR to front-end clients to disable the "Select Tier" button in real time.
