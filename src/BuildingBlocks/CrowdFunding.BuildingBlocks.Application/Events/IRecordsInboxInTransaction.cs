namespace CrowdFunding.BuildingBlocks.Application.Events;

/// <summary>
/// Marks an event handler whose work is one module transaction. The module's transaction executor
/// then writes the inbox row in that same transaction, so the handler's effect and the record
/// that it ran commit or roll back together and a crash cannot leave a handler run unrecorded.
/// Do not put it on a handler that commits more than one transaction: a retry after the first
/// commit would skip the rest. Handlers without it are recorded after they finish.
/// </summary>
public interface IRecordsInboxInTransaction
{
}
