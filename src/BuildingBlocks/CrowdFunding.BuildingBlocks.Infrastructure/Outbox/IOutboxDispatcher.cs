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

    /// <summary>
    /// Processes batches until no row is due and none is in flight in another worker (for
    /// example the background processor woken by a commit), or <paramref name="timeout"/> elapses.
    /// Rows rescheduled for a later retry are not waited for.
    /// </summary>
    /// <param name="timeout">The longest time to keep draining.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the outbox was drained; <see langword="false"/> on timeout.</returns>
    Task<bool> DrainAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
