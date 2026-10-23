namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Scoped carrier for the id of the outbox message currently being dispatched. The outbox
/// processor sets it before publishing; the event publisher reads it to apply the inbox. It is
/// empty for events published outside an outbox dispatch, which then bypass the inbox.
/// </summary>
public sealed class InboxMessageContext
{
    public Guid? MessageId { get; set; }
}
