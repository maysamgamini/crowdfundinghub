# Poly-pattern tier assignment for the real modules

The idea is in `educational/01-architecture-paradigms/poly-pattern-architecture.md` and the runnable
comparison is `src/Samples/RosettaStone/`. This document applies it to the six real modules: which
feature belongs in which tier, how that was decided, and what is still unfinished.

## The two questions that decide a tier

1. Does another module need to react to this change reliably? In the code that means the aggregate
   raises an event that goes through the outbox.
2. Can two concurrent writes corrupt this data? In the code that means an `xmin` concurrency token,
   an advisory lock, or a unique key that makes a repeat safe.

If both answers are no, the feature does not need the full pipeline (controller, command dispatcher,
handler, aggregate, repository, outbox).

| Tier | Shape | Use it when |
|---|---|---|
| 1 | One endpoint that validates and uses the module's `DbContext` | No rules beyond validation, no events, no concurrency risk |
| 2 | Endpoint, command and handler, `DbContext` used directly (no aggregate, no repository) | A little logic worth its own place and a unit test, still no events and no concurrency risk |
| 3 | Aggregate, repository, command dispatcher, outbox, locks and tokens | Either question above is yes |

Rule that does not change by tier: a module's endpoint may only use its own `DbContext`, its own
schema, and other modules' Contracts. Tiers change how much code sits inside the module, never where
the module boundary is.

## What the code says today

Only three aggregates raise events: `Campaign`, `Contribution` and `CampaignReview`. Six
entities carry an `xmin` token: `Campaign`, `Contribution`, `RefreshToken`, `WebhookSubscription`, `RewardTier` and `RewardTierReservation`.

| Module | Write features | Today | Proposed tier |
|---|---|---|---|
| Contributions | MakeContribution, ConfirmContributionPayment, FailContributionPayment, ReconcilePaymentWebhook | Tier 3 | Stay 3 (money, `xmin`, unique-key webhook guard, events) |
| Campaigns | AddContributionToCampaign, ReserveRewardTierSlot, ReleaseExpiredRewardTierReservation, Publish, Cancel, Complete, Fail, CreateCampaign | Tier 3 | Stay 3 (advisory lock, `xmin`, ledger, events) |
| Campaigns | CreateRewardTier | Tier 3 | Candidate for Tier 2: confirm it raises no event and needs no lock |
| Moderation | CreateCampaignReview, Approve, Reject | Tier 3 | Stay 3 (`CampaignReview` raises events) |
| Moderation | RecordMediaAnalysis | Tier 3 | Candidate for Tier 2: check whether it raises an event |
| Notifications | UpdateNotificationPreferences, Unsubscribe, GetNotificationPreferences | Tier 2 (done): handlers in Infrastructure use NotificationsDbContext directly; one INSERT ... ON CONFLICT write | Tier 2 |
| CampaignUpdates | RegisterWebhookSubscription, DeleteWebhookSubscription | Tier 3 shape | Stays Tier 3. Checked and rejected as a Tier 2 candidate: the dispatcher reads a subscription, makes an HTTP call of up to 5 seconds, then saves it back, and a stale save re-activated a subscription the owner had just deleted (reproduced by `WebhookSubscriptionConcurrencyTests`). It now has an `xmin` token, so that question 2 ("can concurrent writes corrupt this data?") is answered yes |
| Identity | Login, Logout, RefreshAccessToken, Register, AssignRole, GrantPermission, Deactivate, SeedAdmin | Tier 3 shape | Stay 3 for refresh tokens (`xmin`); the rest need review, security rules apply |
| All modules | Queries | Query handlers, several read through `AsNoTracking` | Already the fast read path; no change |

"Candidate" means the classification still has to be confirmed in the code before anything moves.

## Lessons from applying the two questions

- Classifying by the shape of the handler is not enough. CampaignUpdates subscriptions looked like plain CRUD, but the dispatcher's long read-modify-write made them a concurrency risk. Test question 2 by looking at every writer of the table, not only the feature's own handlers.
- Remaining gap: if the dispatcher's save lands between the owner's load and save in the delete handler, the owner's request now gets a `DbUpdateConcurrencyException` (an HTTP 500) instead of a clean 409.

## What is not done yet

- Only Notifications preferences have been converted. Every other write feature still uses the Tier 3 shape.
- The Notifications boundary test checks constructor dependencies and assembly references for that module only; there is no general rule yet for every Tier 1 or Tier 2 feature.
