using System.Text.Json;
using CrowdFunding.BuildingBlocks.Application.Events;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Represents a persisted application event waiting to be published from the outbox.
/// See docs/outbox-architecture.md for the full design rationale (SKIP LOCKED vs. Debezium CDC,
/// poison-message handling, multi-instance safety).
/// </summary>
public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, string eventType, int version, string payload, DateTime occurredOnUtc)
    {
        Id = id;
        EventType = eventType;
        Version = version;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
        ScheduledAtUtc = occurredOnUtc;
        Status = OutboxMessageStatus.Pending;
        Attempts = 0;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable discriminator — the event class's simple name, not an assembly-qualified
    /// CLR type name. See <see cref="EventTypeRegistry"/> for why.</summary>
    public string EventType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int Attempts { get; private set; }
    public OutboxMessageStatus Status { get; private set; }

    /// <summary>When this row becomes eligible to be claimed again — "now" for a fresh message,
    /// pushed into the future on retry so a failing message backs off instead of being
    /// re-claimed on the very next poll.</summary>
    public DateTime ScheduledAtUtc { get; private set; }

    /// <summary>Set by the SKIP LOCKED claim query while a worker holds this row; cleared when
    /// the row leaves Processing (processed, retried, or dead-lettered).</summary>
    public string? LockedBy { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }

    /// <summary>
    /// Creates a new <see cref="OutboxMessage"/> by serializing the specified domain or application event.
    /// </summary>
    /// <param name="applicationEvent">The event object to serialize and persist in the outbox.</param>
    /// <param name="occurredOnUtc">The UTC timestamp when the event occurred.</param>
    /// <returns>A new <see cref="OutboxMessage"/> initialized in <see cref="OutboxMessageStatus.Pending"/> status.</returns>
    public static OutboxMessage Create(object applicationEvent, DateTime occurredOnUtc)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        var eventType = applicationEvent.GetType();
        var discriminator = EventTypeRegistry.GetDiscriminator(eventType);
        var version = EventTypeRegistry.GetVersion(eventType);
        var payload = JsonSerializer.Serialize(applicationEvent, eventType, SerializerOptions);

        return new OutboxMessage(Guid.NewGuid(), discriminator, version, payload, occurredOnUtc);
    }

    /// <summary>
    /// Attempts to resolve and deserialize this row's payload via the given registry. Returns
    /// false with a reason instead of throwing when the (EventType, Version) is unrecognized or
    /// the payload doesn't parse — the caller routes that to the dead-letter table rather than
    /// retrying forever (improvement.md §2.3's "silent drop" / fragile-reflection defects both
    /// become a visible, terminal DeadLetter row instead).
    /// </summary>
    public bool TryResolve(EventTypeRegistry registry, out object? applicationEvent, out string? failureReason)
    {
        if (!registry.TryResolve(EventType, Version, out var runtimeType) || runtimeType is null)
        {
            applicationEvent = null;
            failureReason = $"No registered event type for EventType='{EventType}', Version={Version}.";
            return false;
        }

        try
        {
            applicationEvent = JsonSerializer.Deserialize(Payload, runtimeType, SerializerOptions);

            if (applicationEvent is null)
            {
                failureReason = "Deserialization produced a null event.";
                return false;
            }

            failureReason = null;
            return true;
        }
        catch (JsonException exception)
        {
            applicationEvent = null;
            failureReason = $"Payload failed to deserialize: {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// Marks the outbox message as successfully processed and clears worker locking metadata.
    /// </summary>
    /// <param name="processedOnUtc">The UTC timestamp when processing completed.</param>
    public void MarkProcessed(DateTime processedOnUtc)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedOnUtc = processedOnUtc;
        Error = null;
        LockedBy = null;
        LockedUntilUtc = null;
    }

    /// <summary>
    /// Records a processing failure. Below <paramref name="maxAttempts"/> the row goes back to
    /// Pending with its <see cref="ScheduledAtUtc"/> pushed out by <paramref name="retryDelay"/>
    /// (backoff); at or past the limit it moves to the terminal DeadLetter status instead of
    /// being retried forever — this is what eliminates the head-of-line blocking in
    /// improvement.md §2.1 (previously: no attempts cutoff + `break`, so one poison message
    /// froze the entire module's outbox indefinitely).
    /// </summary>
    public void MarkFailed(string error, int maxAttempts, TimeSpan retryDelay, DateTime nowUtc)
    {
        Attempts++;
        Error = string.IsNullOrWhiteSpace(error) ? "Unknown outbox processing error." : error.Trim();
        LockedBy = null;
        LockedUntilUtc = null;

        if (Attempts >= maxAttempts)
        {
            Status = OutboxMessageStatus.DeadLetter;
        }
        else
        {
            Status = OutboxMessageStatus.Pending;
            ScheduledAtUtc = nowUtc.Add(retryDelay);
        }
    }

    /// <summary>Routes a row straight to DeadLetter without consuming a retry — used when the
    /// event type/version is unrecognized or the payload can't be parsed, since retrying an
    /// unresolvable message can never succeed without a code change.</summary>
    public void MarkDeadLetter(string reason)
    {
        Status = OutboxMessageStatus.DeadLetter;
        Error = reason;
        LockedBy = null;
        LockedUntilUtc = null;
    }
}
