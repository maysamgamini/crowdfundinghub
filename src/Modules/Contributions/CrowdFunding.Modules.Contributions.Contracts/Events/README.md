# Events

## Purpose
Groups the application events the Contributions module publishes through its outbox.

## Files
- No direct files live in this folder; it primarily organizes child folders.

## Child Folders
- `ContributionPaymentConfirmed`: `ContributionPaymentConfirmedApplicationEvent`, published when a payment is confirmed (consumed by Campaigns' ledger, Notifications, CampaignUpdates webhooks, and metering).
- `ContributionPaymentFailed`: `ContributionPaymentFailedApplicationEvent`, published when a payment fails (releases the reward-tier reservation).
- `ContributionRefunded`: `ContributionRefundedApplicationEvent`, published for each refund (Campaigns appends a compensating ledger entry and releases the reward-tier slot; Notifications alerts the backer).
