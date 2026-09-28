namespace CrowdFunding.BuildingBlocks.Application.Inbox;

/// <summary>
/// The consumer side of the transactional outbox (an "inbox"): applies a message's effect at most
/// once per consumer, however many times the message is delivered.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is at-least-once. The outbox retries a message whenever any of its handlers fails,
/// so every other handler of that message runs again; a broker redelivers after a lost ack; a
/// payment gateway resends webhooks for days. A handler whose effect is not naturally idempotent
/// (it appends rows or calls out) wraps that effect in <see cref="ExecuteOnceAsync"/>.
/// </para>
/// <para>
/// Each module exposes its own derived interface (for example
/// <c>ICampaignUpdatesIdempotentConsumer</c>) backed by its own DbContext, because the
/// processed-event record must commit in the same transaction as the effect it guards.
/// </para>
/// </remarks>
public interface IIdempotentConsumer
{
    /// <summary>
    /// Records that <paramref name="consumerName"/> processed <paramref name="eventId"/> and runs
    /// <paramref name="effect"/>, both in one database transaction. When the record already
    /// exists, the effect is skipped.
    /// </summary>
    /// <param name="eventId">The message identity (an application event's <c>EventId</c>, or an external webhook's event id).</param>
    /// <param name="consumerName">A stable name for the consumer; one message can be processed once by each consumer.</param>
    /// <param name="effect">The state change to apply once.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the effect ran; <see langword="false"/> when the message was already processed.</returns>
    Task<bool> ExecuteOnceAsync(
        string eventId,
        string consumerName,
        Func<CancellationToken, Task> effect,
        CancellationToken cancellationToken);
}
