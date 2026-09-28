namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Provides a common base type for application events (the integration events modules exchange
/// through the outbox).
/// </summary>
public abstract class BaseApplicationEvent
{
    /// <summary>
    /// Gets the event's identity. Assigned once when the event is raised and serialized with the
    /// payload, so every redelivery (outbox retry, broker redelivery, a second replica) carries the
    /// same value. Consumers use it as the key for
    /// <see cref="Inbox.IIdempotentConsumer"/>; the outbox row and the CloudEvents <c>id</c> reuse it.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();
}
