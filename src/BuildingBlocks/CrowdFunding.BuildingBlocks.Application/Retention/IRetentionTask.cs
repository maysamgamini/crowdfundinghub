namespace CrowdFunding.BuildingBlocks.Application.Retention;

/// <summary>
/// One piece of routine cleanup: data that has a useful life and must not grow without bound. Each
/// module registers the tasks for its own tables; one background service runs them all on a schedule.
/// </summary>
public interface IRetentionTask
{
    /// <summary>A short name for logs.</summary>
    string Name { get; }

    /// <summary>Cleans up what has outlived its retention as of <paramref name="nowUtc"/> and returns how many rows it touched.</summary>
    Task<int> RunAsync(IServiceProvider scopedServices, DateTime nowUtc, CancellationToken cancellationToken);
}
