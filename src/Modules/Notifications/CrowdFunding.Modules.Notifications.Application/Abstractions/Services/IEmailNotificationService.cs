namespace CrowdFunding.Modules.Notifications.Application.Abstractions.Services;

/// <summary>
/// Sends transactional email notifications to backers and creators. Implementations perform real
/// external network I/O (SendGrid, AWS SES, SMTP) — callers are always invoked from an
/// <c>IEventHandler&lt;T&gt;</c> running inside the *producing* module's outbox worker
/// (<c>ModuleOutboxProcessor&lt;TDbContext&gt;</c>), so a thrown exception here is not swallowed:
/// it propagates back through <c>IMessageBus.PublishAsync</c> to the outbox loop, which already
/// applies exponential backoff and eventual dead-lettering (TICKET-038) — the exact dual-write
/// defense this ticket exists to prove out. Implementations must therefore let transient failures
/// throw rather than catch-and-log them, or that defense never engages.
/// </summary>
public interface IEmailNotificationService
{
    /// <summary>Sends a backer their pledge/payment confirmation receipt.</summary>
    /// <param name="recipientUserId">The contributor's user id — resolving this to an actual
    /// mailbox address is an infrastructure-implementation concern (see remarks on
    /// <see cref="CrowdFunding.Modules.Notifications.Application.Abstractions.Persistence.ICampaignTitleCacheRepository"/>
    /// for why Notifications never queries Identity directly for it).</param>
    /// <param name="campaignId">The campaign the pledge was made against.</param>
    /// <param name="contributionId">The contribution id — the natural idempotency key for this
    /// notification, since a contribution is confirmed exactly once.</param>
    /// <param name="amount">The pledged amount.</param>
    /// <param name="currency">The three-letter ISO 4217 currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendContributionReceiptAsync(
        Guid recipientUserId,
        Guid campaignId,
        Guid contributionId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>Alerts a backer that a campaign they pledged to was cancelled and their pledge
    /// was refunded.</summary>
    /// <param name="recipientUserId">The affected backer's user id.</param>
    /// <param name="campaignId">The cancelled campaign.</param>
    /// <param name="contributionId">The refunded contribution id — the idempotency key.</param>
    /// <param name="refundedAmount">The amount refunded.</param>
    /// <param name="currency">The three-letter ISO 4217 currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendCampaignCancellationAlertAsync(
        Guid recipientUserId,
        Guid campaignId,
        Guid contributionId,
        decimal refundedAmount,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Alerts a backer that an update was posted for a campaign they backed (Commercial / Informational notification).
    /// Respects the backer's notification preferences and includes compliant RFC 8058 List-Unsubscribe headers (TICKET-056).
    /// </summary>
    Task SendCampaignUpdateAlertAsync(
        Guid recipientUserId,
        Guid campaignId,
        Guid updateId,
        string updateTitle,
        CancellationToken cancellationToken = default);
}
