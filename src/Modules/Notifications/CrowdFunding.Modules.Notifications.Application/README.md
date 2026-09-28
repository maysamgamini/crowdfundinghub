# Crowd Funding Modules Notifications Application

## Purpose
Contains the application-layer orchestration for the Notifications module, including handlers, validators, and service abstractions.

## Files
- `CrowdFunding.Modules.Notifications.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Abstractions`: Declares persistence repositories (`INotificationPreferenceRepository`, `ICampaignTitleCacheRepository`) and service contracts (`IEmailNotificationService`, `INotificationsDateTimeProvider`).
- `DependencyInjection`: Registers the preference query and command handlers with the dependency injection container.
- `Events`: Event handlers reacting to other modules' application events (`ContributionPaymentConfirmedNotificationHandler` and `ContributionRefundedNotificationHandler` send email; the campaign and review handlers are currently no-ops).
- `Features`: CQRS slices implementing notification business operations: `Preferences` (the `GetNotificationPreferencesQuery` handler, `UpdateNotificationPreferencesCommand`, `UnsubscribeCommand`) and `ReplicatedCampaigns` (`ReplicatedCampaignTitleEventHandler`, which fills the local campaign title cache).
