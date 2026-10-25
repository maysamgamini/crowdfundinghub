namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Scoped carrier for the id of the outbox message currently being dispatched. The outbox
/// processor sets it before publishing; the event publisher reads it to apply the inbox. It is
/// empty for events published outside an outbox dispatch, which then bypass the inbox.
/// </summary>
public sealed class InboxMessageContext
{
    public Guid? MessageId { get; set; }

    /// <summary>
    /// Set by the publisher while it runs a handler that implements
    /// <see cref="IRecordsInboxInTransaction"/>: the consumer name the handler's own transaction
    /// must record in the inbox. Empty for every other handler.
    /// </summary>
    public string? ConsumerToRecordInTransaction { get; set; }

    /// <summary>True once a transaction of the running handler wrote the inbox row.</summary>
    public bool RecordedInTransaction { get; set; }
}
