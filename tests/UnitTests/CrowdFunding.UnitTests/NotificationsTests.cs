using System.Net;
using CrowdFunding.Modules.Contributions.Contracts.Events.ContributionPaymentConfirmed;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using CrowdFunding.Modules.Notifications.Application.Events;
using CrowdFunding.Modules.Notifications.Infrastructure.Services;

namespace CrowdFunding.UnitTests;

/// <summary>
/// TICKET-031: the dual-write defense the outbox pattern provides only works if a failed
/// notification send actually throws instead of being swallowed — these tests verify that
/// contract directly, without needing a full HTTP round trip through the outbox worker (already
/// covered end-to-end by <c>EmailNotificationOutboxE2ETests</c>).
/// </summary>
public sealed class NotificationsTests
{
    private sealed class ThrowingEmailNotificationService : IEmailNotificationService
    {
        public Task SendContributionReceiptAsync(
            Guid recipientUserId, Guid campaignId, Guid contributionId, decimal amount, string currency, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated 503 from the email provider.");

        public Task SendCampaignCancellationAlertAsync(
            Guid recipientUserId, Guid campaignId, Guid contributionId, decimal refundedAmount, string currency, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated 503 from the email provider.");

        public Task SendCampaignUpdateAlertAsync(
            Guid recipientUserId, Guid campaignId, Guid updateId, string updateTitle, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated 503 from the email provider.");
    }

    [Fact]
    public async Task ContributionPaymentConfirmedNotificationHandler_ShouldPropagate_EmailProviderFailure()
    {
        // A propagating exception is what lets ModuleOutboxProcessor's existing MarkFailed
        // backoff/dead-letter machinery (TICKET-038) engage — the underlying contribution/payment
        // database write is already committed by this point and is never rolled back by this
        // failure, since the email send happens strictly after that transaction, driven by the
        // outbox worker reading the already-committed row.
        var handler = new ContributionPaymentConfirmedNotificationHandler(new ThrowingEmailNotificationService());
        var notification = new ContributionPaymentConfirmedApplicationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "USD");

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.Handle(notification, CancellationToken.None));
    }

    private sealed class NullCampaignTitleCacheRepository : ICampaignTitleCacheRepository
    {
        public Task UpsertAsync(Guid campaignId, string title, Guid ownerId, DateTime updatedAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<CampaignTitleSnapshot?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
            => Task.FromResult<CampaignTitleSnapshot?>(null);
    }

    private sealed class NullNotificationPreferenceRepository : INotificationPreferenceRepository
    {
        private readonly Dictionary<Guid, CrowdFunding.Modules.Notifications.Domain.Aggregates.NotificationPreference> _store = new();

        public Task<CrowdFunding.Modules.Notifications.Domain.Aggregates.NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(userId, out var pref);
            return Task.FromResult(pref);
        }

        public Task UpsertAsync(CrowdFunding.Modules.Notifications.Domain.Aggregates.NotificationPreference preference, CancellationToken cancellationToken = default)
        {
            _store[preference.UserId] = preference;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedDateTimeProvider : INotificationsDateTimeProvider
    {
        public DateTime UtcNow { get; } = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }
    }

    [Fact]
    public async Task HttpEmailNotificationService_ShouldSendXMessageIdIdempotencyHeader_DerivedFromContributionId()
    {
        var recordingHandler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(recordingHandler) { BaseAddress = new Uri("https://email-provider.test") };
        var service = new HttpEmailNotificationService(httpClient, new NullCampaignTitleCacheRepository(), new NullNotificationPreferenceRepository());

        var contributionId = Guid.NewGuid();
        await service.SendContributionReceiptAsync(Guid.NewGuid(), Guid.NewGuid(), contributionId, 100m, "USD");

        Assert.NotNull(recordingHandler.LastRequest);
        var header = Assert.Single(recordingHandler.LastRequest!.Headers.GetValues("X-Message-Id"));
        Assert.Equal($"contribution-{contributionId}", header);
    }

    [Fact]
    public async Task HttpEmailNotificationService_ShouldThrow_WhenTheProviderReturnsAnErrorStatus()
    {
        var failingHandlerHttpClient = new HttpClient(new FailingHttpMessageHandler()) { BaseAddress = new Uri("https://email-provider.test") };
        var service = new HttpEmailNotificationService(failingHandlerHttpClient, new NullCampaignTitleCacheRepository(), new NullNotificationPreferenceRepository());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SendContributionReceiptAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "USD"));
    }

    [Fact]
    public async Task HttpEmailNotificationService_ShouldIncludeListUnsubscribeHeaders_OnCampaignUpdates()
    {
        var recordingHandler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(recordingHandler) { BaseAddress = new Uri("https://email-provider.test") };
        var service = new HttpEmailNotificationService(httpClient, new NullCampaignTitleCacheRepository(), new NullNotificationPreferenceRepository());

        var userId = Guid.NewGuid();
        await service.SendCampaignUpdateAlertAsync(userId, Guid.NewGuid(), Guid.NewGuid(), "Major milestone achieved!");

        Assert.NotNull(recordingHandler.LastRequest);
        Assert.True(recordingHandler.LastRequest!.Headers.Contains("List-Unsubscribe"));
        Assert.True(recordingHandler.LastRequest!.Headers.Contains("List-Unsubscribe-Post"));
    }

    [Fact]
    public async Task HttpEmailNotificationService_ShouldSuppressCampaignUpdate_WhenUserOptedOut()
    {
        var recordingHandler = new RecordingHttpMessageHandler();
        using var httpClient = new HttpClient(recordingHandler) { BaseAddress = new Uri("https://email-provider.test") };
        var repo = new NullNotificationPreferenceRepository();
        var userId = Guid.NewGuid();
        var pref = CrowdFunding.Modules.Notifications.Domain.Aggregates.NotificationPreference.CreateDefault(userId, DateTime.UtcNow);
        pref.Update(campaignUpdatesEnabled: false, marketingAnnouncementsEnabled: false, DateTime.UtcNow);
        await repo.UpsertAsync(pref);

        var service = new HttpEmailNotificationService(httpClient, new NullCampaignTitleCacheRepository(), repo);

        await service.SendCampaignUpdateAlertAsync(userId, Guid.NewGuid(), Guid.NewGuid(), "Major milestone achieved!");

        // Suppressed! No HTTP call made
        Assert.Null(recordingHandler.LastRequest);
    }

    [Fact]
    public async Task NotificationPreferences_QueryAndCommandHandlers_ShouldManagePreferencesCorrectly()
    {
        var repo = new NullNotificationPreferenceRepository();
        var clock = new FixedDateTimeProvider();
        var getHandler = new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences.GetNotificationPreferencesQueryHandler(repo, clock);
        var updateHandler = new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences.UpdateNotificationPreferencesCommandHandler(repo, clock);
        var unsubHandler = new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe.UnsubscribeCommandHandler(repo, clock);

        var userId = Guid.NewGuid();

        // 1. Defaults when not yet saved (campaign updates default true, marketing announcements default false per GDPR)
        var initial = await getHandler.Handle(new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences.GetNotificationPreferencesQuery(userId), CancellationToken.None);
        Assert.True(initial.CampaignUpdatesEnabled);
        Assert.False(initial.MarketingAnnouncementsEnabled);

        // 2. Explicit update
        var updated = await updateHandler.Handle(new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.UpdateNotificationPreferences.UpdateNotificationPreferencesCommand(userId, false, true), CancellationToken.None);
        Assert.False(updated.CampaignUpdatesEnabled);
        Assert.True(updated.MarketingAnnouncementsEnabled);

        // 3. One-click unsubscribe
        var unsub = await unsubHandler.Handle(new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Commands.Unsubscribe.UnsubscribeCommand(userId), CancellationToken.None);
        Assert.True(unsub.Unsubscribed);

        var finalState = await getHandler.Handle(new CrowdFunding.Modules.Notifications.Application.Features.Preferences.Queries.GetNotificationPreferences.GetNotificationPreferencesQuery(userId), CancellationToken.None);
        Assert.False(finalState.CampaignUpdatesEnabled);
        Assert.False(finalState.MarketingAnnouncementsEnabled);
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
