# Crowd Funding Modules Notifications Infrastructure

## Purpose
Contains infrastructure implementations for the Notifications module, including EF Core persistence and the email providers. It has no outbox and no transaction executor, because the module publishes no events.

## Files
- `CrowdFunding.Modules.Notifications.Infrastructure.csproj`: Project file that defines dependencies, target framework, and assembly references for this area.

## Child Folders
- `DependencyInjection`: Registers `NotificationsDbContext` (connection string `ConnectionStrings:NotificationsDb`, falling back to `DefaultConnection`; migration history in the `notifications` schema), the repositories, the clock, and the email provider selected by `Notifications:EmailProvider`.
- `Persistence`: Contains `NotificationsDbContext`, EF Core entity configurations (`NotificationPreferenceConfiguration`, `CampaignTitleCacheConfiguration`), the replicated read model (`CampaignTitleCache`, table `campaign_title_cache`), migrations, and repositories (`NotificationPreferenceRepository`, `CampaignTitleCacheRepository`).
- `Services`: Implements `HttpEmailNotificationService` (with RFC 8058 `List-Unsubscribe` headers, an `X-Message-Id` idempotency key, and preference enforcement), `LoggingEmailNotificationService` (the default, which logs and records sends into `IEmailNotificationSink` for tests), `EmailProviderOptions`, and `NotificationsDateTimeProvider`.
