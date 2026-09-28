using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace CrowdFunding.BuildingBlocks.Infrastructure.Outbox;

/// <summary>
/// Wakes one module's outbox processor as soon as a transaction that wrote outbox rows commits,
/// instead of leaving the rows until the next poll. Ported from apps/backend's
/// <c>ChannelOutboxSignal</c>, but one instance per module DbContext, because each module here
/// runs its own processor.
/// </summary>
/// <remarks>
/// A single-slot bounded channel with <see cref="BoundedChannelFullMode.DropWrite"/>: any number
/// of commits between two processor passes coalesce into one wake-up, with no queue growth. The
/// signal is in-memory, so it only wakes the processor in the replica that committed; other
/// replicas, and rows written by another process, are still picked up by the processor's
/// fallback sweep.
/// </remarks>
/// <typeparam name="TDbContext">The module DbContext whose outbox this signal serves.</typeparam>
public sealed class OutboxSignal<TDbContext>
    where TDbContext : DbContext
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    /// <summary>
    /// Signals that committed outbox rows are ready. Non-blocking and coalescing.
    /// </summary>
    public void Notify() => _channel.Writer.TryWrite(true);

    /// <summary>
    /// Waits until <see cref="Notify"/> is called or <paramref name="fallbackInterval"/> elapses.
    /// </summary>
    /// <param name="fallbackInterval">The longest time to wait before a fallback sweep.</param>
    /// <param name="cancellationToken">Stops the wait when the host shuts down.</param>
    /// <returns><see langword="true"/> when woken by a commit; <see langword="false"/> on timeout.</returns>
    public async Task<bool> WaitAsync(TimeSpan fallbackInterval, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(fallbackInterval);

        try
        {
            await _channel.Reader.ReadAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
