namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// One row per (outbox message, handler) that has finished, stored in the consuming module's own
/// schema. The composite primary key makes a second insert of the same pair a no-op.
/// </summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; set; }

    /// <summary>Full name of the handler class that processed the message.</summary>
    public string Consumer { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; }
}
