# Events

## Purpose
Contains the handlers that react to other modules' application events.

## Files
- `NotificationEventHandlers.cs`: One handler class per event: `ContributionPaymentConfirmedNotificationHandler` (pledge receipt) and `ContributionRefundedNotificationHandler` (refund/cancellation alert) send email through `IEmailNotificationService`; `CampaignPublishedNotificationHandler`, `CampaignCancelledNotificationHandler`, `CampaignReviewApprovedNotificationHandler`, and `CampaignReviewRejectedNotificationHandler` are currently no-ops.

## Child Folders
- None.
