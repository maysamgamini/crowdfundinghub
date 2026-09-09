# Crowd Funding Modules Notifications Infrastructure

## Purpose
Contains infrastructure implementations for the Notifications module, including EF Core persistence, services, and transaction executors.

## Files
- `CrowdFunding.Modules.Notifications.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers notifications infrastructure services, HttpClient handlers, and repositories.
- `Persistence`: Contains `NotificationsDbContext`, EF Core entity configurations (`NotificationPreferenceConfiguration`), migrations, and repositories (`NotificationPreferenceRepository`, `CampaignTitleCacheRepository`).
- `Services`: Implements `HttpEmailNotificationService` (with RFC 8058 `List-Unsubscribe` headers, idempotency keys, and preference enforcement), `LoggingEmailNotificationService`, and `NotificationsDateTimeProvider`.
