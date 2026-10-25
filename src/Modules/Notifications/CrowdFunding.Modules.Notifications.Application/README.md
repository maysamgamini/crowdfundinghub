# Crowd Funding Modules Notifications Application

## Purpose
Contains the application-layer orchestration for the Notifications module, including handlers, validators, and service abstractions.

## Files
- `CrowdFunding.Modules.Notifications.Application.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `Abstractions`: Declares persistence repositories (`INotificationPreferenceRepository`, `ICampaignTitleCacheRepository`) and service contracts (`IEmailNotificationService`, `INotificationsDateTimeProvider`).
- `DependencyInjection`: Registers Notifications services, handlers, validators, and infrastructure components with the dependency injection container.
- `Events`: Groups event handlers reacting to outbox messages (e.g. `PledgePaymentConfirmedNotificationHandler`, `CampaignCancellationNotificationHandler`, `CampaignUpdateNotificationHandler`).
- `Features`: only the command and query **types** for preferences (`GetNotificationPreferencesQuery`, `UpdateNotificationPreferencesCommand`, `UnsubscribeCommand`). Their handlers are Tier 2 and live in `Notifications.Infrastructure/Features/Preferences`, so they can use `NotificationsDbContext` directly; `Program.cs` registers them with `AddRequestHandlersFromAssemblies`.
