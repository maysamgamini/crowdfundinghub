# Notifications Module

## Purpose
The Notifications module consumes integration events across all modules and dispatches multi-channel user alerts (such as email, push, or console summaries).

## Capabilities & Event Subscriptions
The module listens for core business milestones via `NotificationEventHandlers`:
- `CampaignPublishedApplicationEvent`: Alerts followers that a campaign is officially live and accepting pledges.
- `CampaignCancelledApplicationEvent`: Alerts backers that a campaign has been terminated.
- `ContributionPaymentConfirmedApplicationEvent`: Dispatches pledge receipts and thank-you notifications to backers and campaign creators.
- `CampaignReviewApprovedApplicationEvent`: Informs campaign creators that their campaign has passed moderation review and is ready to publish.
- `CampaignReviewRejectedApplicationEvent`: Delivers moderation rejection notices and reviewer feedback to creators.

## Projects & Layers
- `CrowdFunding.Modules.Notifications.Domain`: Houses core notification rules and event models.
- `CrowdFunding.Modules.Notifications.Application`: Orchestrates notification event handlers and channel dispatchers.
- `CrowdFunding.Modules.Notifications.Infrastructure`: Dispatches notifications via console/external transport integrations.
