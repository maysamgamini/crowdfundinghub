# Campaign Updates & Activity Feed Module

## Purpose
The Campaign Updates module consumes campaign lifecycle and contribution milestones to build real-time activity timelines, backer update streams, and public audit history.

## Capabilities & Event Subscriptions
Subscribes to system events via `CampaignActivityEventHandlers`:
- `CampaignCreatedApplicationEvent`: Records initial draft registration in the public audit ledger.
- `CampaignPublishedApplicationEvent`: Posts public launch announcement into the campaign's activity feed.
- `CampaignCancelledApplicationEvent`: Appends cancellation event and timestamp to the campaign timeline.
- `ContributionPaymentConfirmedApplicationEvent`: Emits funding milestone progress and backer acknowledgment entries.
- `CampaignReviewApprovedApplicationEvent` & `CampaignReviewRejectedApplicationEvent`: Records moderation decision timeline records.

## Projects & Layers
- `CrowdFunding.Modules.CampaignUpdates.Domain`: Models campaign timeline entries and activity status.
- `CrowdFunding.Modules.CampaignUpdates.Application`: Orchestrates activity event handlers and query abstractions.
- `CrowdFunding.Modules.CampaignUpdates.Infrastructure`: Manages update feeds and persistent timeline sinks.
