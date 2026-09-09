using System.Collections.Concurrent;

namespace CrowdFunding.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// One notification actually sent by <see cref="LoggingEmailNotificationService"/>. The
/// <see cref="IdempotencyKey"/> mirrors the <c>X-Message-Id</c> header a real HTTP provider
/// (see <see cref="HttpEmailNotificationService"/>) would send — same value, same purpose:
/// a duplicate delivery (outbox retry, at-least-once redelivery) is identifiable as a repeat of
/// this exact notification rather than a new one.
/// </summary>
public sealed record SentEmailNotification(
    string IdempotencyKey,
    string Kind,
    Guid RecipientUserId,
    Guid CampaignId,
    decimal Amount,
    string Currency,
    DateTime SentAtUtc);

/// <summary>
/// An in-memory record of every notification "sent" by <see cref="LoggingEmailNotificationService"/>
/// — the dev/test fallback the ticket spec calls for ("storing to an in-memory test sink"),
/// registered as a singleton so integration tests can assert on delivery without a real mail
/// provider. Never used by <see cref="HttpEmailNotificationService"/>, which performs real
/// outbound HTTP calls instead.
/// </summary>
public interface IEmailNotificationSink
{
    IReadOnlyCollection<SentEmailNotification> SentMessages { get; }

    void Record(SentEmailNotification message);
}

/// <inheritdoc/>
public sealed class InMemoryEmailNotificationSink : IEmailNotificationSink
{
    private readonly ConcurrentQueue<SentEmailNotification> _sentMessages = new();

    public IReadOnlyCollection<SentEmailNotification> SentMessages => _sentMessages.ToArray();

    public void Record(SentEmailNotification message) => _sentMessages.Enqueue(message);
}
