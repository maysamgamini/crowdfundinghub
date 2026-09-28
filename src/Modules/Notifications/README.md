# Notifications Module

## Purpose
The Notifications module consumes other modules' application events and sends transactional email to backers, and stores each user's notification preferences (opt-ins for campaign updates and marketing). It is a pure event sink: it publishes no events of its own and therefore has no outbox.

## Capabilities & Event Subscriptions
Event handlers live in `Application/Events/NotificationEventHandlers.cs` (one class per event) and run inside the producing module's outbox worker, so a failed email send throws and the outbox retries it:
- `ContributionPaymentConfirmedNotificationHandler` (`ContributionPaymentConfirmedApplicationEvent`): Sends the backer a pledge receipt.
- `ContributionRefundedNotificationHandler` (`ContributionRefundedApplicationEvent`): Sends the backer the cancellation/refund alert, once per refunded contribution.
- `CampaignPublishedNotificationHandler`, `CampaignCancelledNotificationHandler`, `CampaignReviewApprovedNotificationHandler`, `CampaignReviewRejectedNotificationHandler`: Registered subscriptions that currently do nothing (`CampaignCancelledApplicationEvent` carries no backer list, which is why the backer alert reacts to the per-contribution refund event instead).
- `ReplicatedCampaignTitleEventHandler` (`CampaignCreatedApplicationEvent`): Replicates a campaign's title and owner into the local `campaign_title_cache`, so emails can name the campaign without a synchronous cross-module call.

## Preferences
`GetNotificationPreferencesQuery` (public contract in `Notifications.Contracts`), `UpdateNotificationPreferencesCommand`, and `UnsubscribeCommand` (one-click, anonymous; it currently trusts a bare user id, a known limitation). Transactional email (receipts, refund notices) ignores preferences.

## Persistence
One `NotificationsDbContext` owning `notification_preferences` and `campaign_title_cache`, together with its EF migrations history, in the `notifications` schema. `ConnectionStrings:NotificationsDb` falls back to `DefaultConnection`.

## Projects & Layers
- `CrowdFunding.Modules.Notifications.Contracts`: `GetNotificationPreferencesQuery` and its result.
- `CrowdFunding.Modules.Notifications.Domain`: The `NotificationPreference` aggregate.
- `CrowdFunding.Modules.Notifications.Application`: Event handlers, preference commands and queries, and the ports they use.
- `CrowdFunding.Modules.Notifications.Infrastructure`: EF Core persistence and the email providers (`HttpEmailNotificationService`, `LoggingEmailNotificationService`).
