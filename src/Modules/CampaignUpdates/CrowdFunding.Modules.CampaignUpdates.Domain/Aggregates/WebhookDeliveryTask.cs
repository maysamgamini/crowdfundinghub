namespace CrowdFunding.Modules.CampaignUpdates.Domain.Aggregates;

public enum WebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Dead = 3,
}

/// <summary>
/// One outbound webhook delivery to dispatch — deliberately its own durable row rather than an
/// in-memory fan-out, so a creator's misconfigured or hostile endpoint can never turn into lost
/// deliveries or a synchronous call from the request thread that raised the underlying pledge
/// event. See TICKET-035.
/// </summary>
public sealed class WebhookDeliveryTask
{
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
    ];

    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public DateTime ScheduledAtUtc { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public string? LastError { get; private set; }

    private WebhookDeliveryTask()
    {
    }

    private WebhookDeliveryTask(Guid id, Guid subscriptionId, string eventType, string payloadJson, DateTime scheduledAtUtc)
    {
        Id = id;
        SubscriptionId = subscriptionId;
        EventType = eventType;
        PayloadJson = payloadJson;
        Attempts = 0;
        ScheduledAtUtc = scheduledAtUtc;
        Status = WebhookDeliveryStatus.Pending;
    }

    public static WebhookDeliveryTask Create(Guid subscriptionId, string eventType, string payloadJson, DateTime nowUtc)
        => new(Guid.NewGuid(), subscriptionId, eventType, payloadJson, nowUtc);

    public void MarkDelivered()
    {
        Status = WebhookDeliveryStatus.Delivered;
        LastError = null;
    }

    /// <summary>
    /// Records a failed delivery attempt. Below <see cref="BackoffSchedule"/>'s length, the task
    /// stays <see cref="WebhookDeliveryStatus.Pending"/> with <see cref="ScheduledAtUtc"/> pushed
    /// out by the matching backoff step (1m, 5m, 15m, 1h); once exhausted, it moves to the
    /// terminal <see cref="WebhookDeliveryStatus.Dead"/> state — this ticket's "archived in the
    /// Dead-Letter table with the final HTTP error payload" criterion, satisfied by the row
    /// itself rather than a separate table, exactly as TICKET-038 established for the generic
    /// outbox's own dead-letter rows.
    /// </summary>
    public void MarkFailed(string error, DateTime nowUtc)
    {
        Attempts++;
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown delivery error." : error.Trim();

        if (Attempts > BackoffSchedule.Length)
        {
            Status = WebhookDeliveryStatus.Dead;
        }
        else
        {
            Status = WebhookDeliveryStatus.Pending;
            ScheduledAtUtc = nowUtc.Add(BackoffSchedule[Attempts - 1]);
        }
    }
}
