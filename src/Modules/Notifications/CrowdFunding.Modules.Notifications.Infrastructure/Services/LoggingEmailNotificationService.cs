using CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence;
using CrowdFunding.Modules.Notifications.Application.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Development/test fallback for <see cref="IEmailNotificationService"/>: writes a structured
/// log line and records the send into an <see cref="IEmailNotificationSink"/> instead of calling
/// a real provider. Selected by default (<c>Notifications:EmailProvider</c> unset or
/// <c>"Logging"</c>) so local development and the test suite never need real SendGrid/SES
/// credentials — see <see cref="HttpEmailNotificationService"/> for the production provider.
/// </summary>
public sealed class LoggingEmailNotificationService : IEmailNotificationService
{
    private readonly ICampaignTitleCacheRepository _campaignTitleCache;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IEmailNotificationSink _sink;
    private readonly ILogger<LoggingEmailNotificationService> _logger;

    public LoggingEmailNotificationService(
        ICampaignTitleCacheRepository campaignTitleCache,
        INotificationPreferenceRepository preferenceRepository,
        IEmailNotificationSink sink,
        ILogger<LoggingEmailNotificationService> logger)
    {
        _campaignTitleCache = campaignTitleCache;
        _preferenceRepository = preferenceRepository;
        _sink = sink;
        _logger = logger;
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

    public async Task SendCampaignUpdateAlertAsync(
        Guid recipientUserId,
        Guid campaignId,
        Guid updateId,
        string updateTitle,
        CancellationToken cancellationToken = default)
    {
        var prefs = await _preferenceRepository.GetByUserIdAsync(recipientUserId, cancellationToken);
        if (prefs is not null && !prefs.CampaignUpdatesEnabled)
        {
            _logger.LogInformation(
                "Skipping campaign update email to user {RecipientUserId} for campaign {CampaignId} due to user preferences (TICKET-056).",
                recipientUserId, campaignId);
            return;
        }

        await SendAsync("CampaignUpdateAlert", $"update-{updateId}-{recipientUserId}", recipientUserId, campaignId, 0m, "N/A", cancellationToken);
    }

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

        _logger.LogInformation(
            "Sending {Kind} email (X-Message-Id: {IdempotencyKey}) to user {RecipientUserId} for campaign {CampaignId} ({CampaignTitle}): {Amount} {Currency}",
            kind, idempotencyKey, recipientUserId, campaignId, campaignTitle, amount, currency);

        _sink.Record(new SentEmailNotification(idempotencyKey, kind, recipientUserId, campaignId, amount, currency, DateTime.UtcNow));
    }
}
