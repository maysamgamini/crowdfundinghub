namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// A durable record of an outbox message that could not be processed — either because its
/// (EventType, Version) is unrecognized/unparseable, or because it exhausted its retry budget.
/// Moving a poison message here (instead of leaving it stuck Pending) is what breaks the
/// head-of-line block: the outbox query only ever looks at Pending rows, so a DeadLetter row is
/// permanently out of the way of every message behind it, and stays available here for manual
/// inspection/replay.
/// </summary>
public sealed class DeadLetterEvent
{
    private DeadLetterEvent()
    {
    }

    public DeadLetterEvent(
        Guid sourceEventId,
        string eventType,
        int version,
        string payload,
        string failureReason,
        DateTime recordedAtUtc)
    {
        Id = Guid.NewGuid();
        SourceEventId = sourceEventId;
        EventType = eventType;
        Version = version;
        Payload = payload;
        FailureReason = failureReason;
        RecordedAtUtc = recordedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string FailureReason { get; private set; } = string.Empty;
    public DateTime RecordedAtUtc { get; private set; }
}
