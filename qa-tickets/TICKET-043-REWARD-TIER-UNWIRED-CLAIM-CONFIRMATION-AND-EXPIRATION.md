# QA Ticket: TICKET-043

**Title:** Disconnected Reward Tier Reservation Lifecycle: Dead Code in `ConfirmClaim`/`ReleaseReservation` & Indefinite Inventory Lockout  
**Severity:** 🔴 P1 (Critical - Inventory Reservation Sagas & Dead Business Logic)  
**QA Focus Area:** Domain Aggregate Lifecycle, Inventory Management & Cross-Module Sagas  
**Found By:** `qa-domain-verification`  
**Status:** Open  
**Project Mode:** Greenfield (Benchmark Educational Standard)  

---

## 1. Description & Architectural Context

In TICKET-034, the `RewardTier` domain entity was introduced to manage limited perk inventories (e.g. 50 Early Bird rewards). The domain entity defines three core lifecycle operations:
- `ReserveSlot()`: Increments `ReservedCount`, decrements `AvailableCount`.
- `ConfirmClaim()`: Decrements `ReservedCount`, increments `ClaimedCount`.
- `ReleaseReservation()`: Decrements `ReservedCount`, returning the slot to `AvailableCount`.

### The Lifecycle Disconnect
A comprehensive audit of the codebase reveals that **`ConfirmClaim()` and `ReleaseReservation()` are never called in application code**:
1. When a backer selects a reward perk, [`ReserveRewardTierSlotCommandHandler`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/RewardTiers/Commands/ReserveRewardTierSlot/ReserveRewardTierSlotCommandHandler.cs#L63) calls `rewardTier.ReserveSlot()`. `ReservedCount` becomes 1.
2. The backer proceeds to checkout and makes a contribution. However, [`Contribution`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs) **does not store `RewardTierId`**!
3. When the backer's payment succeeds, `ContributionPaymentConfirmedApplicationEvent` is published. Neither `AddContributionToCampaignCommandHandler` nor any other handler resolves the reward tier reservation.
4. Consequently:
   - `ConfirmClaim()` is **DEAD CODE** (only executed in isolated unit tests).
   - `ClaimedCount` is **PERMANENTLY 0** in production databases!
   - Every confirmed contribution leaves its reward tier slot in `ReservedCount = 1` indefinitely!
5. Even worse: If a backer reserves a reward perk slot and abandons checkout (or their payment card declines), **`ReleaseReservation()` is NEVER called**. There is no reservation expiry timestamp (`ReservedUntilUtc`) and no background scavenger worker to release abandoned slots.

---

## 2. Blast Radius & Business Impact

- **Permanent Inventory Exhaustion (Denial-of-Inventory):** Because abandoned checkouts never release their reservation, a malicious user or automated bot can call `POST /api/campaigns/{id}/reward-tiers/{id}/reserve` repeatedly, locking all 50 slots in minutes and permanently preventing legitimate backers from claiming rewards.
- **Corrupted Reporting & Fulfillment:** Creators and fulfillment teams query `ClaimedCount` to determine manufacturing batches. Because `ClaimedCount` remains 0, physical manufacturing and perk shipment logic receives completely broken data.

---

## 3. Educational Rationale: Teaching Principals & Architects

### The Pedagogical Objective
Teach architects how to properly implement **Two-Phase Inventory Reservation Sagas (Reserve -> Confirm / Expire)** across modular boundaries. Reserving an inventory slot is only step 1; the architecture must provide:
1. A correlation mechanism (attaching `RewardTierId` to the `Contribution`),
2. A saga confirmation step (converting `Reserved` to `Claimed` upon payment confirmation), and
3. A compensating expiration worker (releasing reservations if payment is not completed within 15 minutes).

### Monolith First, Microservices Ready
In e-commerce and ticketing architectures, inventory reservation is a classic distributed saga problem. By establishing the 15-minute reservation window with automated expiration and correlation to payment state inside the Modular Monolith, developers learn the exact pattern used by Amazon, Ticketmaster, and Kickstarter.

---

## 4. Affected Files & Modules

- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Domain/Aggregates/RewardTier.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Domain/Aggregates/Contribution.cs)
- [`src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionPaymentConfirmed/ContributionPaymentConfirmedApplicationEvent.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Contributions/CrowdFunding.Modules.Contributions.Contracts/Events/ContributionPaymentConfirmed/ContributionPaymentConfirmedApplicationEvent.cs)
- [`src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/AddContributionToCampaign/AddContributionToCampaignCommandHandler.cs`](file:///Users/maysamgamini/maysam-brain/Maysam's%20Brain/projects/projects-active/crowdfunding/src/Modules/Campaigns/CrowdFunding.Modules.Campaigns.Application/Features/Campaigns/Commands/AddContributionToCampaign/AddContributionToCampaignCommandHandler.cs)

---

## 5. Greenfield Remediation Guidance

1. **Attach `RewardTierId` to Contribution:** Add optional `Guid? RewardTierId` to `Contribution.Create()`, `MakeContributionCommand`, and `ContributionPaymentConfirmedApplicationEvent`.
2. **Track Reservation Expiration:** Add `DateTime? ReservedUntilUtc` and `Guid? ReservedByUserId` to `RewardTierSlotReservation` or `RewardTier`. Default TTL to 15 minutes (`DateTime.UtcNow.AddMinutes(15)`).
3. **Wire Confirmation:** When `ContributionPaymentConfirmedApplicationEvent` is processed by Campaigns, if `RewardTierId.HasValue`, load the tier under advisory lock and invoke `rewardTier.ConfirmClaim()`.
4. **Automated Reservation Scavenger:** Introduce `RewardTierReservationScavengerBackgroundService` running every 1–2 minutes to query expired reservations (`ReservedUntilUtc < UtcNow`) and invoke `ReleaseReservation()`.

---

## 6. Verification & Acceptance Criteria

1. **Successful Checkout Converts to Claimed:** When a contribution with `RewardTierId` is confirmed, `RewardTier.ClaimedCount` increments by 1 and `ReservedCount` returns to 0.
2. **Abandoned Reservation Reclaimed:** An unconfirmed reservation older than 15 minutes is automatically released by the background scavenger, returning the slot to `AvailableCount`.
