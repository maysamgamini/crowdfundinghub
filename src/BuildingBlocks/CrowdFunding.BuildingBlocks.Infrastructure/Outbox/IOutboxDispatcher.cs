namespace CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Marker implemented by every module's outbox background service, so callers that need to
/// trigger a claim-and-publish pass — production diagnostics, and integration tests that need
/// outbox processing to happen deterministically instead of waiting on a poll interval — can do
/// so without knowing which concrete module processors exist or referencing their DbContexts.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>Runs one claim-and-publish pass over this dispatcher's outbox table immediately.</summary>
    Task ProcessBatchAsync(CancellationToken cancellationToken);
}
