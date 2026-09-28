namespace CrowdFunding.BuildingBlocks.Infrastructure.Inbox;

/// <summary>
/// Durable record that a consumer has already applied a message. The composite primary key
/// (<see cref="EventId"/>, <see cref="ConsumerName"/>) is what makes redelivery a no-op: the
/// database, not application code, refuses a second record.
/// </summary>
/// <remarks>
/// Ported from apps/backend's <c>ProcessedEvent</c>. <see cref="EventId"/> is a string rather
/// than a <see cref="Guid"/> so external message ids (for example Stripe's <c>evt_…</c>) use the
/// same table as internal application events.
/// </remarks>
public sealed class ProcessedEvent
{
    /// <summary>Gets the message identity.</summary>
    public string EventId { get; private init; } = string.Empty;

    /// <summary>Gets the name of the consumer that processed the message.</summary>
    public string ConsumerName { get; private init; } = string.Empty;

    /// <summary>Gets the UTC time the message was processed.</summary>
    public DateTime ProcessedAtUtc { get; private init; }
}
