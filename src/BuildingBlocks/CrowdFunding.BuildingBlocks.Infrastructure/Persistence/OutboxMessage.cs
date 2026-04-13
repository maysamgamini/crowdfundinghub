using System.Text.Json;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Represents a persisted application event waiting to be published from the outbox.
/// </summary>
public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, string eventType, string payload, DateTime occurredOnUtc)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        OccurredOnUtc = occurredOnUtc;
        Attempts = 0;
    }

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int Attempts { get; private set; }

    public static OutboxMessage Create(object applicationEvent, DateTime occurredOnUtc)
    {
        ArgumentNullException.ThrowIfNull(applicationEvent);

        var eventType = applicationEvent.GetType().AssemblyQualifiedName
                        ?? throw new InvalidOperationException("Application event type name could not be resolved.");

        var payload = JsonSerializer.Serialize(applicationEvent, applicationEvent.GetType(), SerializerOptions);

        return new OutboxMessage(Guid.NewGuid(), eventType, payload, occurredOnUtc);
    }

    public object Deserialize()
    {
        var runtimeType = Type.GetType(EventType, throwOnError: true)
                          ?? throw new InvalidOperationException($"Unable to resolve outbox event type '{EventType}'.");

        return JsonSerializer.Deserialize(Payload, runtimeType, SerializerOptions)
               ?? throw new InvalidOperationException($"Unable to deserialize outbox event '{Id}'.");
    }

    public void MarkProcessed(DateTime processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        Error = string.IsNullOrWhiteSpace(error) ? "Unknown outbox processing error." : error.Trim();
    }
}
