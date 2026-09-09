namespace CrowdFunding.BuildingBlocks.Application.Audit;

/// <summary>
/// Persists <see cref="AuditRecord"/>s. A write that fails is not swallowed — unlike best-effort
/// caching or realtime-notification concerns elsewhere in this codebase, a silently-lost audit
/// record defeats the entire point of forensic non-repudiation, so a caller
/// (<see cref="AuditLoggingPipelineBehavior{TCommand,TResult}"/>) that cannot record one must
/// treat that as a failure of the command itself, not a degraded-but-acceptable side effect.
/// </summary>
public interface IAuditStore
{
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken);
}
