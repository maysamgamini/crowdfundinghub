# Crowd Funding Modules Notifications Application

## Purpose
Contains the application-layer orchestration for the Notifications module, including handlers, validators, and service abstractions.

## Files
- `CrowdFunding.Modules.Notifications.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Abstractions`: Declares persistence repositories (`INotificationPreferenceRepository`, `ICampaignTitleCacheRepository`) and service contracts (`IEmailNotificationService`, `INotificationsDateTimeProvider`).
- `DependencyInjection`: Registers Notifications services, handlers, validators, and infrastructure components with the dependency injection container.
- `Events`: Groups event handlers reacting to outbox messages (e.g. `ContributionPaymentConfirmedNotificationHandler`, `CampaignCancellationNotificationHandler`, `CampaignUpdateNotificationHandler`).
- `Features`: CQRS slices implementing notification business operations (e.g. `Preferences` with `GetNotificationPreferencesQuery`, `UpdateNotificationPreferencesCommand`, `UnsubscribeCommand`).
