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
        var service = new HttpEmailNotificationService(httpClient, new NullCampaignTitleCacheRepository());

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
        var service = new HttpEmailNotificationService(failingHandlerHttpClient, new NullCampaignTitleCacheRepository());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SendContributionReceiptAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, "USD"));
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
