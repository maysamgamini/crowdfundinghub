using System.Net.Http.Json;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Production <see cref="IEmailNotificationService"/> provider: a real outbound HTTP call to an
/// external email API (SendGrid/AWS SES-shaped — POST a JSON payload, Bearer auth), registered as
/// a typed <c>HttpClient</c> with .NET's standard resilience handler (retry + circuit breaker +
/// timeout), the same pattern <c>OpenMeterClient</c> uses. Every request carries an
/// <c>X-Message-Id</c> idempotency header derived from the natural business key (contribution
/// id) so a retried delivery — either this handler's own resilience-handler retry, or the outbox
/// worker retrying the whole event after a transient failure — is recognizable by the receiving
/// email API as a duplicate rather than a second, distinct send.
/// <para>
/// Unlike <c>OpenMeterClient</c>, which deliberately swallows failures because metering must
/// never block the business pipeline, this class lets <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
/// throw on failure — that exception is the dual-write defense TICKET-031 exists to prove: it
/// propagates back through the outbox worker, which applies backoff and eventual dead-lettering
/// (TICKET-038) rather than silently losing the notification.
/// </para>
/// </summary>
public sealed class HttpEmailNotificationService : IEmailNotificationService
{
    private const string SendPath = "/v3/mail/send";
    private readonly HttpClient _httpClient;
    private readonly ICampaignTitleCacheRepository _campaignTitleCache;

    public HttpEmailNotificationService(HttpClient httpClient, ICampaignTitleCacheRepository campaignTitleCache)
    {
        _httpClient = httpClient;
        _campaignTitleCache = campaignTitleCache;
    }

    public Task SendContributionReceiptAsync(
        Guid recipientUserId,
        Guid campaignId,
        Guid contributionId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default)
        => SendAsync("ContributionReceipt", $"contribution-{contributionId}", recipientUserId, campaignId, amount, currency, cancellationToken);

    public Task SendCampaignCancellationAlertAsync(
        Guid recipientUserId,
        Guid campaignId,
        Guid contributionId,
        decimal refundedAmount,
        string currency,
        CancellationToken cancellationToken = default)
        => SendAsync("CampaignCancellationAlert", $"cancellation-{contributionId}", recipientUserId, campaignId, refundedAmount, currency, cancellationToken);

    private async Task SendAsync(
        string kind,
        string idempotencyKey,
        Guid recipientUserId,
        Guid campaignId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignTitleCache.GetAsync(campaignId, cancellationToken);
        var campaignTitle = campaign?.Title ?? "your campaign";

        using var request = new HttpRequestMessage(HttpMethod.Post, SendPath)
        {
            Content = JsonContent.Create(new
            {
                kind,
                recipientUserId,
                campaignId,
                campaignTitle,
                amount,
                currency,
            }),
        };
        request.Headers.Add("X-Message-Id", idempotencyKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
