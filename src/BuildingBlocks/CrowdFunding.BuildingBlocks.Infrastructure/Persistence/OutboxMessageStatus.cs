namespace CrowdFunding.BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Lifecycle of an outbox row. A row only ever moves Pending -&gt; Processing -&gt; (Processed |
/// Pending [retry] | DeadLetter) -- it is never mutated back into Pending from Processed, and
/// DeadLetter is terminal.
/// </summary>
public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    DeadLetter = 3,
}
